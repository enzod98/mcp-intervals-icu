using System.ComponentModel;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;

namespace IntervalsMcp.Tools;

[McpServerToolType]
public static class SummaryTools
{
    [McpServerTool, Description(
        "Calcula un resumen agregado de entrenamiento para un rango de fechas: volumen y carga total, desglose por tipo " +
        "de deporte, y evolución de CTL/ATL. Intervals.icu no tiene un endpoint de resumen personal, así que esta " +
        "herramienta lo arma sumando list_activities y list_wellness — útil para repasos semanales/mensuales sin sumar a mano.")]
    public static async Task<string> GetPeriodSummary(
        IntervalsIcuClient client,
        [Description("Fecha más antigua del período, ISO-8601 (ej. 2026-09-01).")] string oldest,
        [Description("Fecha más reciente del período, ISO-8601 (ej. 2026-09-21). Si se omite, se usa hoy.")] string? newest = null,
        CancellationToken ct = default)
    {
        var activitiesJson = await client.ListActivitiesAsync(oldest, newest, 500, ct);
        var wellnessJson = await client.ListWellnessAsync(oldest, newest, ct);
        return BuildSummary(oldest, newest, activitiesJson, wellnessJson);
    }

    private static string BuildSummary(string oldest, string? newest, string activitiesJson, string wellnessJson)
    {
        var byType = new Dictionary<string, TypeTotals>();
        var overall = new TypeTotals();
        var daysWithActivity = new HashSet<string>();

        if (JsonNode.Parse(activitiesJson) is JsonArray activities)
        {
            foreach (var activity in activities)
            {
                if (activity is not JsonObject a) continue;

                var type = a["type"]?.GetValue<string>() ?? "Unknown";
                var totals = byType.TryGetValue(type, out var existing) ? existing : byType[type] = new TypeTotals();

                var distance = GetDouble(a["distance"]);
                var movingTime = GetDouble(a["moving_time"]);
                var load = GetDouble(a["icu_training_load"]);

                totals.Count++;
                totals.DistanceMeters += distance;
                totals.MovingTimeSecs += movingTime;
                totals.TrainingLoad += load;
                overall.Count++;
                overall.DistanceMeters += distance;
                overall.MovingTimeSecs += movingTime;
                overall.TrainingLoad += load;

                if (a["start_date_local"]?.GetValue<string>() is { Length: >= 10 } startDate)
                {
                    daysWithActivity.Add(startDate[..10]);
                }
            }
        }

        double? ctlStart = null, ctlEnd = null, atlStart = null, atlEnd = null;
        if (JsonNode.Parse(wellnessJson) is JsonArray wellness && wellness.Count > 0)
        {
            var first = wellness[0] as JsonObject;
            var last = wellness[^1] as JsonObject;
            ctlStart = GetNullableDouble(first?["ctl"]);
            atlStart = GetNullableDouble(first?["atl"]);
            ctlEnd = GetNullableDouble(last?["ctl"]);
            atlEnd = GetNullableDouble(last?["atl"]);
        }

        var result = new JsonObject
        {
            ["period"] = new JsonObject { ["oldest"] = oldest, ["newest"] = newest },
            ["days_with_activity"] = daysWithActivity.Count,
            ["overall"] = ToJson(overall),
            ["by_type"] = new JsonObject(byType.Select(kv =>
                new KeyValuePair<string, JsonNode?>(kv.Key, ToJson(kv.Value)))),
            ["fitness_trend"] = new JsonObject
            {
                ["ctl_start"] = ctlStart,
                ["ctl_end"] = ctlEnd,
                ["ctl_change"] = ctlStart is not null && ctlEnd is not null ? Math.Round(ctlEnd.Value - ctlStart.Value, 1) : null,
                ["atl_start"] = atlStart,
                ["atl_end"] = atlEnd,
            },
        };

        return result.ToJsonString();
    }

    private static double GetDouble(JsonNode? node) => GetNullableDouble(node) ?? 0;

    private static double? GetNullableDouble(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<double>(out var d) ? d : null;

    private static JsonObject ToJson(TypeTotals totals) => new()
    {
        ["count"] = totals.Count,
        ["distance_km"] = Math.Round(totals.DistanceMeters / 1000, 1),
        ["moving_time_hours"] = Math.Round(totals.MovingTimeSecs / 3600, 1),
        ["training_load"] = Math.Round(totals.TrainingLoad, 0),
    };

    private class TypeTotals
    {
        public int Count;
        public double DistanceMeters;
        public double MovingTimeSecs;
        public double TrainingLoad;
    }
}
