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

    private async Task<string> GetAsync(string requestUri, CancellationToken ct)
    {
        var response = await http.GetAsync(requestUri, ct);
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
