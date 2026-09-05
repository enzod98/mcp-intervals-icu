namespace IntervalsMcp.Tools;

/// <summary>Intervals.icu rechaza con un 422 las consultas por rango de fechas si se omite "oldest", así que las herramientas usan este valor por defecto.</summary>
internal static class DefaultDates
{
    public static string OldestFallback(int daysBack = 90) =>
        DateTime.UtcNow.AddDays(-daysBack).ToString("yyyy-MM-dd");
}
