using System.ComponentModel;
using ModelContextProtocol.Server;

namespace IntervalsMcp.Tools;

[McpServerToolType]
public static class WellnessTools
{
    [McpServerTool, Description(
        "Lista las entradas diarias de wellness del atleta en Intervals.icu para un rango de fechas: FC en reposo, HRV, sueño, " +
        "peso, y métricas de carga de entrenamiento (CTL/ATL/ramp rate).")]
    public static Task<string> ListWellness(
        IntervalsIcuClient client,
        [Description("Fecha más antigua a incluir, formato ISO-8601 (ej. 2026-08-01). Si se omite, no hay límite inferior.")] string? oldest = null,
        [Description("Fecha más reciente a incluir, formato ISO-8601 (ej. 2026-09-05). Si se omite, se usa hoy.")] string? newest = null,
        CancellationToken ct = default)
        => client.ListWellnessAsync(oldest, newest, ct);

    [McpServerTool, Description("Obtiene la entrada de wellness del atleta para una fecha puntual en Intervals.icu.")]
    public static Task<string> GetWellness(
        IntervalsIcuClient client,
        [Description("La fecha a consultar, formato ISO-8601 (ej. 2026-09-05).")] string date,
        CancellationToken ct = default)
        => client.GetWellnessAsync(date, ct);
}
