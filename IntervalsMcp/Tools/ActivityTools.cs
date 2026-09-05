using System.ComponentModel;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;

namespace IntervalsMcp.Tools;

[McpServerToolType]
public static class ActivityTools
{
    [McpServerTool, Description("Lista las actividades de entrenamiento registradas del atleta (ciclismo, running, natación, etc.) en Intervals.icu, de más reciente a más antigua.")]
    public static Task<string> ListActivities(
        IntervalsIcuClient client,
        [Description("Fecha más antigua a incluir, formato ISO-8601 (ej. 2026-08-01). Por defecto, hace 90 días; Intervals.icu exige este parámetro.")] string? oldest = null,
        [Description("Fecha más reciente a incluir, formato ISO-8601 (ej. 2026-09-05). Si se omite, se usa hoy.")] string? newest = null,
        [Description("Cantidad máxima de actividades a devolver. Por defecto, 30.")] int limit = 30,
        CancellationToken ct = default)
        => client.ListActivitiesAsync(oldest ?? DefaultDates.OldestFallback(), newest, limit, ct);

    [McpServerTool, Description("Obtiene el detalle completo de una actividad de Intervals.icu: carga de entrenamiento calculada, TSS, distribución en zonas de potencia/FC/ritmo, y desglose por vueltas/intervalos.")]
    public static Task<string> GetActivity(
        IntervalsIcuClient client,
        [Description("El id de la actividad en Intervals.icu, ej. i12345678.")] string activityId,
        [Description("Incluir el desglose por intervalo/vuelta si la actividad lo tiene. Por defecto, true.")] bool includeIntervals = true,
        CancellationToken ct = default)
        => client.GetActivityAsync(activityId, includeIntervals, ct);

    [McpServerTool, Description(
        "Obtiene datos de sensores en serie de tiempo (potencia, frecuencia cardíaca, cadencia, ritmo, altitud, GPS) registrados segundo a segundo " +
        "durante una actividad. Las grabaciones largas se reducen (downsampling) automáticamente para que la respuesta tenga un tamaño manejable.")]
    public static async Task<string> GetActivityStreams(
        IntervalsIcuClient client,
        [Description("El id de la actividad en Intervals.icu, ej. i12345678.")] string activityId,
        [Description("Tipos de stream separados por coma, ej. \"watts,heartrate,cadence,altitude,velocity_smooth,latlng\". Por defecto, watts,heartrate,cadence.")]
        string types = "watts,heartrate,cadence",
        [Description("Cantidad máxima de puntos de datos por stream después del downsampling. Por defecto, 500.")] int maxPoints = 500,
        CancellationToken ct = default)
    {
        var raw = await client.GetActivityStreamsRawAsync(activityId, types, ct);
        return Downsample(raw, maxPoints);
    }

    private static string Downsample(string json, int maxPoints)
    {
        var node = JsonNode.Parse(json);
        switch (node)
        {
            case JsonArray streams:
                foreach (var stream in streams)
                {
                    DownsampleStream(stream, maxPoints);
                }

                break;
            case JsonObject singleStream:
                DownsampleStream(singleStream, maxPoints);
                break;
        }

        return node?.ToJsonString() ?? json;
    }

    private static void DownsampleStream(JsonNode? stream, int maxPoints)
    {
        if (stream is not JsonObject obj || obj["data"] is not JsonArray data)
        {
            return;
        }

        var originalLength = data.Count;
        if (originalLength <= maxPoints)
        {
            return;
        }

        var step = (double)originalLength / maxPoints;
        var sampled = new JsonArray();
        for (var i = 0; i < maxPoints; i++)
        {
            var element = data[(int)(i * step)];
            sampled.Add(element is null ? null : JsonNode.Parse(element.ToJsonString()));
        }

        obj["data"] = sampled;
        obj["original_length"] = originalLength;
        obj["downsampled"] = true;
    }
}
