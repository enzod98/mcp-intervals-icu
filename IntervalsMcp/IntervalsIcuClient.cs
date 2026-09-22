using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol;

namespace IntervalsMcp;

/// <summary>Configuración del servidor que no forma parte del cliente HTTP en sí.</summary>
public record IntervalsIcuOptions(string AthleteId);

/// <summary>Un evento a crear como parte de una carga en lote con <see cref="IntervalsIcuClient.BatchCreateEventsAsync"/>.</summary>
public record EventInput(
    [property: Description("Fecha y hora local de inicio, formato ISO-8601 (ej. 2026-09-10T07:00:00).")] string StartDateLocal,
    [property: Description("Nombre del evento, ej. \"Series 400m x8\".")] string Name,
    [property: Description("Categoría del evento: WORKOUT (entreno planificado), NOTE, RACE_A/RACE_B/RACE_C, TARGET, etc.")] string Category = "WORKOUT",
    [property: Description("Tipo de deporte, ej. Run, Ride, Swim. Necesario si category es WORKOUT.")] string? Type = null,
    [property: Description("Descripción o estructura del entreno (sintaxis de Intervals.icu), o texto libre si category es NOTE.")] string? Description = null,
    [property: Description("Id externo propio para referenciar este evento después.")] string? ExternalId = null);

/// <summary>
/// Envoltorio delgado sobre la API REST de Intervals.icu (https://intervals.icu/api/v1).
/// Cada llamada devuelve el JSON crudo de la API para que las herramientas MCP se mantengan
/// sincronizadas con la API sin necesidad de mantener modelos de respuesta a mano.
/// </summary>
public class IntervalsIcuClient(HttpClient http, IntervalsIcuOptions options)
{
    public string AthleteId => options.AthleteId;

    public Task<string> GetAthleteProfileAsync(CancellationToken ct = default) =>
        GetAsync($"athlete/{AthleteId}/profile", ct);

    public Task<string> GetSportSettingsAsync(CancellationToken ct = default) =>
        GetAsync($"athlete/{AthleteId}/sport-settings", ct);

    public Task<string> ListGearAsync(CancellationToken ct = default) =>
        GetAsync($"athlete/{AthleteId}/gear", ct);

    public Task<string> UpdateAthleteProfileAsync(
        string? name,
        double? weight,
        string? sex,
        string? city,
        string? state,
        string? country,
        string? timezone,
        string? bio,
        CancellationToken ct = default)
    {
        // A diferencia de "name" en actividades, este endpoint sí hace merge parcial real:
        // se verificó mandando solo {"bio": "..."} y confirmando que Garmin/Strava/icu_api_key
        // volvieron intactos en la respuesta. Por eso acá alcanza con un PUT parcial normal,
        // sin necesidad de leer y reenviar el objeto completo (que incluye credenciales sensibles).
        // Nota: el campo "weight" del schema no persiste nada; el peso real vive en "icu_weight"
        // (verificado empíricamente). "height" tampoco persiste bajo ningún nombre confirmado,
        // por eso no se expone como parámetro.
        var changes = new Dictionary<string, object?>();
        if (name is not null) changes["name"] = name;
        if (weight is not null) changes["icu_weight"] = weight;
        if (sex is not null) changes["sex"] = sex;
        if (city is not null) changes["city"] = city;
        if (state is not null) changes["state"] = state;
        if (country is not null) changes["country"] = country;
        if (timezone is not null) changes["timezone"] = timezone;
        if (bio is not null) changes["bio"] = bio;

        return SendJsonAsync(HttpMethod.Put, $"athlete/{AthleteId}", changes, ct);
    }

    public Task<string> UpdateSportSettingsAsync(
        string sportType,
        int? lthr,
        int? maxHr,
        int[]? hrZones,
        string[]? hrZoneNames,
        double? thresholdPace,
        int? ftp,
        int? indoorFtp,
        int? sweetSpotMin,
        int? sweetSpotMax,
        CancellationToken ct = default)
    {
        var changes = new Dictionary<string, object?>();
        if (lthr is not null) changes["lthr"] = lthr;
        if (maxHr is not null) changes["max_hr"] = maxHr;
        if (hrZones is not null) changes["hr_zones"] = hrZones;
        if (hrZoneNames is not null) changes["hr_zone_names"] = hrZoneNames;
        if (thresholdPace is not null) changes["threshold_pace"] = thresholdPace;
        if (ftp is not null) changes["ftp"] = ftp;
        if (indoorFtp is not null) changes["indoor_ftp"] = indoorFtp;
        if (sweetSpotMin is not null) changes["sweet_spot_min"] = sweetSpotMin;
        if (sweetSpotMax is not null) changes["sweet_spot_max"] = sweetSpotMax;

        var uri = $"athlete/{AthleteId}/sport-settings/{sportType}";
        return MergeAndPutAsync(uri, uri, changes, ct);
    }

    // El PUT de Intervals.icu no garantiza merge parcial real (lo confirmamos con el bug de "name"
    // en actividades). El perfil y sport-settings son objetos grandes con muchos campos ajenos a lo
    // que queremos tocar (credenciales de integraciones, config de sync, etc.), así que acá nunca
    // mandamos un PUT parcial: siempre leemos el objeto completo actual, pisamos solo los campos
    // pedidos, y devolvemos el objeto entero. Así el comportamiento real de la API no importa.
    private async Task<string> MergeAndPutAsync(string getUri, string putUri, Dictionary<string, object?> changes, CancellationToken ct)
    {
        var current = await GetAsync(getUri, ct);
        var node = JsonNode.Parse(current) as JsonObject ?? throw new McpException(
            $"La respuesta de Intervals.icu para '{getUri}' no fue el objeto esperado, no se puede actualizar de forma segura.");

        foreach (var (key, value) in changes)
        {
            node[key] = JsonSerializer.SerializeToNode(value);
        }

        using var request = new HttpRequestMessage(HttpMethod.Put, putUri)
        {
            Content = new StringContent(node.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        var response = await http.SendAsync(request, ct);
        return await ReadOrThrowAsync(response, putUri, ct);
    }

    public Task<string> ListActivitiesAsync(string? oldest, string? newest, int? limit, CancellationToken ct = default) =>
        GetAsync($"athlete/{AthleteId}/activities" + BuildQuery(("oldest", oldest), ("newest", newest), ("limit", limit?.ToString())), ct);

    public Task<string> GetActivityAsync(string activityId, bool includeIntervals, CancellationToken ct = default) =>
        GetAsync($"activity/{activityId}" + BuildQuery(("intervals", includeIntervals ? "true" : null)), ct);

    public Task<string> GetActivityStreamsRawAsync(string activityId, string types, CancellationToken ct = default) =>
        GetAsync($"activity/{activityId}/streams" + BuildQuery(("types", types)), ct);

    public Task<string> ListActivityNotesAsync(string activityId, CancellationToken ct = default) =>
        GetAsync($"activity/{activityId}/messages", ct);

    public Task<string> AddActivityNoteAsync(string activityId, string content, CancellationToken ct = default) =>
        SendJsonAsync(HttpMethod.Post, $"activity/{activityId}/messages", new Dictionary<string, object?> { ["content"] = content }, ct);

    public async Task<string> UpdateActivityAsync(
        string activityId,
        string? name,
        string? description,
        string? type,
        double? perceivedExertion,
        int? feel,
        string[]? tags,
        bool? commute,
        bool? trainer,
        CancellationToken ct = default)
    {
        // Fijamos qué nombre debe quedar ANTES de tocar cualquier otro campo, por si hace falta
        // reponerlo después.
        var nameToKeep = name ?? await GetCurrentActivityNameAsync(activityId, ct);

        var otherChanges = new Dictionary<string, object?>();
        if (description is not null) otherChanges["description"] = description;
        if (type is not null) otherChanges["type"] = type;
        if (perceivedExertion is not null) otherChanges["perceived_exertion"] = perceivedExertion;
        if (feel is not null) otherChanges["feel"] = feel;
        if (tags is not null) otherChanges["tags"] = tags;
        if (commute is not null) otherChanges["commute"] = commute;
        if (trainer is not null) otherChanges["trainer"] = trainer;

        if (otherChanges.Count > 0)
        {
            // Actualizar "description" (u otros campos) junto con "name" en la misma request puede
            // hacer que Intervals.icu resetee el nombre a uno autogenerado (parece un re-sync con
            // Strava, activado por la opción "Update Strava name and description" de la cuenta;
            // confirmado empíricamente incluso mandando "name" explícito junto con "description").
            // Por eso "name" siempre se manda en un PUT separado y al final, que sí queda firme.
            await SendJsonAsync(HttpMethod.Put, $"activity/{activityId}", otherChanges, ct);
        }

        return await SendJsonAsync(
            HttpMethod.Put, $"activity/{activityId}", new Dictionary<string, object?> { ["name"] = nameToKeep }, ct);
    }

    private async Task<string?> GetCurrentActivityNameAsync(string activityId, CancellationToken ct)
    {
        var current = await GetAsync($"activity/{activityId}", ct);
        using var doc = JsonDocument.Parse(current);
        return doc.RootElement.TryGetProperty("name", out var nameProp) ? nameProp.GetString() : null;
    }

    public Task<string> ListWellnessAsync(string? oldest, string? newest, CancellationToken ct = default) =>
        GetAsync($"athlete/{AthleteId}/wellness" + BuildQuery(("oldest", oldest), ("newest", newest)), ct);

    public Task<string> GetWellnessAsync(string date, CancellationToken ct = default) =>
        GetAsync($"athlete/{AthleteId}/wellness/{date}", ct);

    public Task<string> UpdateWellnessAsync(
        string date,
        double? weight,
        int? restingHr,
        double? hrv,
        int? sleepSecs,
        double? sleepScore,
        int? soreness,
        int? fatigue,
        int? stress,
        int? mood,
        int? motivation,
        int? injury,
        int? steps,
        string? comments,
        CancellationToken ct = default)
    {
        var changes = new Dictionary<string, object?>();
        if (weight is not null) changes["weight"] = weight;
        if (restingHr is not null) changes["restingHR"] = restingHr;
        if (hrv is not null) changes["hrv"] = hrv;
        if (sleepSecs is not null) changes["sleepSecs"] = sleepSecs;
        if (sleepScore is not null) changes["sleepScore"] = sleepScore;
        if (soreness is not null) changes["soreness"] = soreness;
        if (fatigue is not null) changes["fatigue"] = fatigue;
        if (stress is not null) changes["stress"] = stress;
        if (mood is not null) changes["mood"] = mood;
        if (motivation is not null) changes["motivation"] = motivation;
        if (injury is not null) changes["injury"] = injury;
        if (steps is not null) changes["steps"] = steps;
        if (comments is not null) changes["comments"] = comments;

        return SendJsonAsync(HttpMethod.Put, $"athlete/{AthleteId}/wellness/{date}", changes, ct);
    }

    public Task<string> ListEventsAsync(string? oldest, string? newest, CancellationToken ct = default) =>
        GetAsync($"athlete/{AthleteId}/events" + BuildQuery(("oldest", oldest), ("newest", newest)), ct);

    public Task<string> GetEventAsync(long eventId, CancellationToken ct = default) =>
        GetAsync($"athlete/{AthleteId}/events/{eventId}", ct);

    public Task<string> CreateEventAsync(
        string startDateLocal, string name, string category, string? type, string? description, string? externalId, CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?>
        {
            ["start_date_local"] = startDateLocal,
            ["name"] = name,
            ["category"] = category,
        };
        if (type is not null) payload["type"] = type;
        if (description is not null) payload["description"] = description;
        if (externalId is not null) payload["external_id"] = externalId;

        return SendJsonAsync(HttpMethod.Post, $"athlete/{AthleteId}/events", payload, ct);
    }

    public Task<string> BatchCreateEventsAsync(EventInput[] events, CancellationToken ct = default)
    {
        var payload = events.Select(e =>
        {
            var dict = new Dictionary<string, object?>
            {
                ["start_date_local"] = e.StartDateLocal,
                ["name"] = e.Name,
                ["category"] = e.Category,
            };
            if (e.Type is not null) dict["type"] = e.Type;
            if (e.Description is not null) dict["description"] = e.Description;
            if (e.ExternalId is not null) dict["external_id"] = e.ExternalId;
            return dict;
        }).ToArray();

        return SendJsonAsync(HttpMethod.Post, $"athlete/{AthleteId}/events/bulk", payload, ct);
    }

    public Task<string> UpdateEventAsync(
        long eventId, string? startDateLocal, string? name, string? type, string? category, string? description, CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object?>();
        if (startDateLocal is not null) payload["start_date_local"] = startDateLocal;
        if (name is not null) payload["name"] = name;
        if (type is not null) payload["type"] = type;
        if (category is not null) payload["category"] = category;
        if (description is not null) payload["description"] = description;

        return SendJsonAsync(HttpMethod.Put, $"athlete/{AthleteId}/events/{eventId}", payload, ct);
    }

    public async Task<string> DeleteEventAsync(long eventId, CancellationToken ct = default)
    {
        var response = await http.DeleteAsync($"athlete/{AthleteId}/events/{eventId}", ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new McpException(
                $"La solicitud a la API de Intervals.icu para borrar el evento {eventId} falló con {(int)response.StatusCode} {response.ReasonPhrase}: {Truncate(body)}");
        }

        return string.IsNullOrWhiteSpace(body) ? $"{{\"deleted\":true,\"id\":{eventId}}}" : body;
    }

    private async Task<string> GetAsync(string requestUri, CancellationToken ct)
    {
        var response = await http.GetAsync(requestUri, ct);
        return await ReadOrThrowAsync(response, requestUri, ct);
    }

    private async Task<string> SendJsonAsync(HttpMethod method, string requestUri, object payload, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, requestUri)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
        };
        var response = await http.SendAsync(request, ct);
        return await ReadOrThrowAsync(response, requestUri, ct);
    }

    private static async Task<string> ReadOrThrowAsync(HttpResponseMessage response, string requestUri, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new McpException(
                $"La solicitud a la API de Intervals.icu a '{requestUri}' falló con {(int)response.StatusCode} {response.ReasonPhrase}: {Truncate(body)}");
        }

        return body;
    }

    // Los cuerpos de error de Intervals.icu son JSON chico, pero por las dudas evitamos volcar
    // una respuesta enorme (ej. una página de error HTML de un proxy) dentro del mensaje de la excepción.
    private static string Truncate(string body, int maxLength = 500) =>
        body.Length <= maxLength ? body : body[..maxLength] + "…";

    private static string BuildQuery(params (string Key, string? Value)[] parameters)
    {
        var parts = parameters
            .Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value!)}")
            .ToArray();

        return parts.Length == 0 ? string.Empty : "?" + string.Join("&", parts);
    }
}
