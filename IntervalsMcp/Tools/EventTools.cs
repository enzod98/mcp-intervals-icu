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
}
