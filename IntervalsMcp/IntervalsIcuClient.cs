using System.Text;
using System.Text.Json;

namespace IntervalsMcp;

/// <summary>Configuración del servidor que no forma parte del cliente HTTP en sí.</summary>
public record IntervalsIcuOptions(string AthleteId);

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

    public Task<string> ListActivitiesAsync(string? oldest, string? newest, int? limit, CancellationToken ct = default) =>
        GetAsync($"athlete/{AthleteId}/activities" + BuildQuery(("oldest", oldest), ("newest", newest), ("limit", limit?.ToString())), ct);

    public Task<string> GetActivityAsync(string activityId, bool includeIntervals, CancellationToken ct = default) =>
        GetAsync($"activity/{activityId}" + BuildQuery(("intervals", includeIntervals ? "true" : null)), ct);

    public Task<string> GetActivityStreamsRawAsync(string activityId, string types, CancellationToken ct = default) =>
        GetAsync($"activity/{activityId}/streams" + BuildQuery(("types", types)), ct);

    public Task<string> ListWellnessAsync(string? oldest, string? newest, CancellationToken ct = default) =>
        GetAsync($"athlete/{AthleteId}/wellness" + BuildQuery(("oldest", oldest), ("newest", newest)), ct);

    public Task<string> GetWellnessAsync(string date, CancellationToken ct = default) =>
        GetAsync($"athlete/{AthleteId}/wellness/{date}", ct);

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
            throw new InvalidOperationException(
                $"La solicitud a la API de Intervals.icu para borrar el evento {eventId} falló con {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
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
            throw new InvalidOperationException(
                $"La solicitud a la API de Intervals.icu a '{requestUri}' falló con {(int)response.StatusCode} {response.ReasonPhrase}: {body}");
        }

        return body;
    }

    private static string BuildQuery(params (string Key, string? Value)[] parameters)
    {
        var parts = parameters
            .Where(p => !string.IsNullOrWhiteSpace(p.Value))
            .Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value!)}")
            .ToArray();

        return parts.Length == 0 ? string.Empty : "?" + string.Join("&", parts);
    }
}
