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

    [McpServerTool, Description(
        "Actualiza metadata de una actividad ya registrada en Intervals.icu: nombre, notas/descripción, tipo de deporte, " +
        "RPE, feel, tags, si fue commute/en rodillo, o el equipo (zapatilla/bici) usado. Solo se modifican los campos que se pasan. " +
        "No permite tocar datos grabados (ritmo, potencia, FC, distancia, GPS) porque esos vienen del dispositivo y son de solo lectura en la API. " +
        "Nota: Intervals.icu no permite actualizar actividades que llegaron sincronizadas desde Strava.")]
    public static Task<string> UpdateActivity(
        IntervalsIcuClient client,
        [Description("El id de la actividad en Intervals.icu, ej. i12345678.")] string activityId,
        [Description("Nuevo nombre de la actividad. Omitir para no cambiarlo.")] string? name = null,
        [Description("Notas/descripción del entreno. Omitir para no cambiarla.")] string? description = null,
        [Description("Nuevo tipo de deporte (Run, Ride, Swim, etc). Omitir para no cambiarlo.")] string? type = null,
        [Description("Esfuerzo percibido (RPE), típicamente 1-10. Omitir para no cambiarlo.")] double? perceivedExertion = null,
        [Description("Cómo se sintió el atleta, escala 1-5 (1 muy bien, 5 muy mal). Omitir para no cambiarlo.")] int? feel = null,
        [Description("Lista de tags para la actividad. Reemplaza los tags existentes. Omitir para no cambiarlos.")] string[]? tags = null,
        [Description("Marcar como trayecto commute. Omitir para no cambiarlo.")] bool? commute = null,
        [Description("Marcar como hecho en rodillo/indoor trainer. Omitir para no cambiarlo.")] bool? trainer = null,
        [Description("Id del equipo (zapatilla/bici, ver list_gear) a asignarle a esta actividad, ej. \"g11714419\". Útil para corregir una asignación errónea de Garmin/Strava. Omitir para no cambiarlo.")] string? gearId = null,
        CancellationToken ct = default)
        => client.UpdateActivityAsync(activityId, name, description, type, perceivedExertion, feel, tags, commute, trainer, gearId, ct);

    [McpServerTool, Description(
        "Lista los comentarios/notas privadas de una actividad en Intervals.icu. A diferencia de la descripción " +
        "(que sincroniza con Strava y es pública), estas notas nunca salen de Intervals.icu.")]
    public static Task<string> ListActivityNotes(
        IntervalsIcuClient client,
        [Description("El id de la actividad en Intervals.icu, ej. i12345678.")] string activityId,
        CancellationToken ct = default)
        => client.ListActivityNotesAsync(activityId, ct);

    [McpServerTool, Description(
        "Deja un comentario/nota privada en una actividad de Intervals.icu. A diferencia de \"description\" en " +
        "update_activity (que sincroniza con Strava y queda pública), esta nota nunca sale de Intervals.icu. " +
        "Usar esta herramienta para notas personales de entrenamiento.")]
    public static Task<string> AddActivityNote(
        IntervalsIcuClient client,
        [Description("El id de la actividad en Intervals.icu, ej. i12345678.")] string activityId,
        [Description("El contenido de la nota.")] string content,
        CancellationToken ct = default)
        => client.AddActivityNoteAsync(activityId, content, ct);

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
