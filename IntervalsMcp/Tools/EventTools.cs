using System.ComponentModel;
using ModelContextProtocol.Server;

namespace IntervalsMcp.Tools;

[McpServerToolType]
public static class EventTools
{
    [McpServerTool, Description(
        "Lista los eventos planificados en el calendario del atleta en Intervals.icu (próximos entrenos, carreras, notas) para un rango de fechas.")]
    public static Task<string> ListEvents(
        IntervalsIcuClient client,
        [Description("Fecha más antigua a incluir, formato ISO-8601 (ej. 2026-09-05). Si se omite, no hay límite inferior.")] string? oldest = null,
        [Description("Fecha más reciente a incluir, formato ISO-8601 (ej. 2026-09-12). Si se omite, no hay límite superior.")] string? newest = null,
        CancellationToken ct = default)
        => client.ListEventsAsync(oldest, newest, ct);

    [McpServerTool, Description("Obtiene el detalle de un evento puntual del calendario de Intervals.icu (entreno planificado, carrera o nota) por su id.")]
    public static Task<string> GetEvent(
        IntervalsIcuClient client,
        [Description("El id numérico del evento en Intervals.icu.")] long eventId,
        CancellationToken ct = default)
        => client.GetEventAsync(eventId, ct);

    [McpServerTool, Description(
        "Crea un evento en el calendario del atleta en Intervals.icu: un entreno planificado, una carrera o una nota. " +
        "Para un entreno estructurado, describí los pasos en \"description\" con la sintaxis de Intervals.icu, ej.: " +
        "\"- 15m 55% Warmup\\n3x\\n- 1m 150%\\n- 1m 50%\\n- 15m 55% Cooldown\".")]
    public static Task<string> CreateEvent(
        IntervalsIcuClient client,
        [Description("Fecha y hora local de inicio, formato ISO-8601 (ej. 2026-09-10T07:00:00).")] string startDateLocal,
        [Description("Nombre del evento, ej. \"Series 400m x8\".")] string name,
        [Description("Categoría del evento: WORKOUT (entreno planificado), NOTE, RACE_A/RACE_B/RACE_C (carrera), TARGET, etc. Por defecto, WORKOUT.")] string category = "WORKOUT",
        [Description("Tipo de deporte, ej. Run, Ride, Swim, WeightTraining. Necesario si category es WORKOUT.")] string? type = null,
        [Description("Descripción o estructura del entreno (ver sintaxis en la descripción de esta herramienta), o texto libre si category es NOTE.")] string? description = null,
        [Description("Id externo propio, útil para poder referenciar o actualizar este evento después sin conocer su id de Intervals.icu.")] string? externalId = null,
        CancellationToken ct = default)
        => client.CreateEventAsync(startDateLocal, name, category, type, description, externalId, ct);

    [McpServerTool, Description("Actualiza un evento existente del calendario en Intervals.icu. Solo se modifican los campos que se pasan; el resto queda sin cambios.")]
    public static Task<string> UpdateEvent(
        IntervalsIcuClient client,
        [Description("El id numérico del evento a actualizar.")] long eventId,
        [Description("Nueva fecha y hora local de inicio, formato ISO-8601. Omitir para no cambiarla.")] string? startDateLocal = null,
        [Description("Nuevo nombre del evento. Omitir para no cambiarlo.")] string? name = null,
        [Description("Nueva categoría del evento (WORKOUT, NOTE, RACE_A, etc). Omitir para no cambiarla.")] string? category = null,
        [Description("Nuevo tipo de deporte (Run, Ride, Swim, etc). Omitir para no cambiarlo.")] string? type = null,
        [Description("Nueva descripción/estructura del entreno. Omitir para no cambiarla.")] string? description = null,
        CancellationToken ct = default)
        => client.UpdateEventAsync(eventId, startDateLocal, name, type, category, description, ct);

    [McpServerTool, Description("Elimina un evento del calendario en Intervals.icu (entreno planificado, carrera o nota). Esta acción no se puede deshacer.")]
    public static Task<string> DeleteEvent(
        IntervalsIcuClient client,
        [Description("El id numérico del evento a eliminar.")] long eventId,
        CancellationToken ct = default)
        => client.DeleteEventAsync(eventId, ct);
}
