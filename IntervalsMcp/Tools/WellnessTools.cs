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

    [McpServerTool, Description(
        "Registra o actualiza la entrada de wellness del atleta para una fecha puntual en Intervals.icu: peso, FC en reposo, " +
        "HRV, sueño, y métricas subjetivas (dolor muscular, fatiga, estrés, ánimo, motivación, lesión). Solo se modifican los " +
        "campos que se pasan. Útil para check-ins diarios sin abrir la app.")]
    public static Task<string> UpdateWellness(
        IntervalsIcuClient client,
        [Description("La fecha de la entrada, formato ISO-8601 (ej. 2026-09-21).")] string date,
        [Description("Peso corporal en kg. Omitir para no cambiarlo.")] double? weight = null,
        [Description("Frecuencia cardíaca en reposo (bpm). Omitir para no cambiarla.")] int? restingHr = null,
        [Description("HRV (ms). Omitir para no cambiarlo.")] double? hrv = null,
        [Description("Duración del sueño en segundos. Omitir para no cambiarlo.")] int? sleepSecs = null,
        [Description("Calidad del sueño, escala 0-100 o la que use tu dispositivo. Omitir para no cambiarla.")] double? sleepScore = null,
        [Description("Dolor muscular, escala 1-4 (1 mejor, 4 peor). Omitir para no cambiarlo.")] int? soreness = null,
        [Description("Fatiga, escala 1-4 (1 mejor, 4 peor). Omitir para no cambiarla.")] int? fatigue = null,
        [Description("Estrés, escala 1-4 (1 mejor, 4 peor). Omitir para no cambiarlo.")] int? stress = null,
        [Description("Ánimo, escala 1-4 (1 mejor, 4 peor). Omitir para no cambiarlo.")] int? mood = null,
        [Description("Motivación, escala 1-4 (1 mejor, 4 peor). Omitir para no cambiarla.")] int? motivation = null,
        [Description("Lesión/molestia, escala 1-4 (1 mejor, 4 peor). Omitir para no cambiarla.")] int? injury = null,
        [Description("Pasos del día. Omitir para no cambiarlos.")] int? steps = null,
        [Description("Comentario libre sobre el día. Omitir para no cambiarlo.")] string? comments = null,
        CancellationToken ct = default)
        => client.UpdateWellnessAsync(
            date, weight, restingHr, hrv, sleepSecs, sleepScore, soreness, fatigue, stress, mood, motivation, injury, steps, comments, ct);
}
