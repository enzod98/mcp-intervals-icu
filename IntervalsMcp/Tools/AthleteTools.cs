using System.ComponentModel;
using System.Text.Json.Nodes;
using ModelContextProtocol.Server;

namespace IntervalsMcp.Tools;

[McpServerToolType]
public static class AthleteTools
{
    // Distancias estándar de carrera (metros) que Intervals.icu usa como checkpoints en sus
    // curvas de ritmo; filtramos la curva completa (cientos de puntos) a solo estas para que la
    // respuesta sea una tabla de PRs legible en vez de un array denso.
    private static readonly double[] StandardRaceDistancesMeters =
        [400, 800, 1000, 1500, 1609.34, 3000, 5000, 7000, 10000, 15000, 21097.5, 30000, 42195];
    [McpServerTool, Description("Obtiene el perfil del atleta en Intervals.icu: nombre, peso, zona horaria y thresholds de fitness actuales.")]
    public static Task<string> GetAthleteProfile(IntervalsIcuClient client, CancellationToken ct = default) =>
        client.GetAthleteProfileAsync(ct);

    [McpServerTool, Description("Obtiene la configuración de zonas por deporte del atleta (FTP, LTHR, zonas de potencia/FC/ritmo) configuradas en Intervals.icu.")]
    public static Task<string> GetSportSettings(IntervalsIcuClient client, CancellationToken ct = default) =>
        client.GetSportSettingsAsync(ct);

    [McpServerTool, Description("Lista el equipo (zapatillas, bicicletas) del atleta en Intervals.icu, con kilometraje y horas acumuladas de cada uno.")]
    public static Task<string> ListGear(IntervalsIcuClient client, CancellationToken ct = default) =>
        client.ListGearAsync(ct);

    [McpServerTool, Description(
        "Actualiza datos básicos del perfil del atleta en Intervals.icu (nombre, peso, sexo, ubicación, zona horaria, bio). " +
        "Solo se modifican los campos que se pasan. No toca configuración de integraciones (Garmin/Strava/etc), credenciales ni preferencias de notificaciones.")]
    public static Task<string> UpdateAthleteProfile(
        IntervalsIcuClient client,
        [Description("Nuevo nombre del atleta. Omitir para no cambiarlo.")] string? name = null,
        [Description("Peso corporal en kg. Omitir para no cambiarlo.")] double? weight = null,
        [Description("Sexo (M/F). Omitir para no cambiarlo.")] string? sex = null,
        [Description("Ciudad. Omitir para no cambiarla.")] string? city = null,
        [Description("Provincia/estado. Omitir para no cambiarlo.")] string? state = null,
        [Description("País. Omitir para no cambiarlo.")] string? country = null,
        [Description("Zona horaria, ej. \"America/Asuncion\". Omitir para no cambiarla.")] string? timezone = null,
        [Description("Biografía/descripción del atleta. Omitir para no cambiarla.")] string? bio = null,
        CancellationToken ct = default)
        => client.UpdateAthleteProfileAsync(name, weight, sex, city, state, country, timezone, bio, ct);

    [McpServerTool, Description(
        "Actualiza los thresholds y zonas de un deporte puntual del atleta en Intervals.icu (LTHR, FC máxima, zonas de FC, " +
        "ritmo de umbral, FTP, sweet spot). Solo se modifican los campos que se pasan; el resto de la configuración de ese deporte queda igual.")]
    public static Task<string> UpdateSportSettings(
        IntervalsIcuClient client,
        [Description("Tipo de deporte a actualizar, ej. Run, Ride, Swim, VirtualRide.")] string sportType,
        [Description("Frecuencia cardíaca de umbral (umbral de lactato por FC). Omitir para no cambiarla.")] int? lthr = null,
        [Description("Frecuencia cardíaca máxima. Omitir para no cambiarla.")] int? maxHr = null,
        [Description("Límites de las zonas de FC, de menor a mayor (ej. [148,168,181,192,200,207,208]). Reemplaza las zonas existentes. Omitir para no cambiarlas.")] int[]? hrZones = null,
        [Description("Nombres de cada zona de FC, en el mismo orden que hrZones. Omitir para no cambiarlos.")] string[]? hrZoneNames = null,
        [Description("Ritmo de umbral en m/s (para Run/Swim). Omitir para no cambiarlo.")] double? thresholdPace = null,
        [Description("Umbral de potencia funcional (FTP) en watts (para Ride). Omitir para no cambiarlo.")] int? ftp = null,
        [Description("FTP indoor/en rodillo, si es distinto al FTP outdoor. Omitir para no cambiarlo.")] int? indoorFtp = null,
        [Description("Límite inferior del rango de sweet spot, como % del FTP. Omitir para no cambiarlo.")] int? sweetSpotMin = null,
        [Description("Límite superior del rango de sweet spot, como % del FTP. Omitir para no cambiarlo.")] int? sweetSpotMax = null,
        CancellationToken ct = default)
        => client.UpdateSportSettingsAsync(sportType, lthr, maxHr, hrZones, hrZoneNames, thresholdPace, ftp, indoorFtp, sweetSpotMin, sweetSpotMax, ct);

    [McpServerTool, Description(
        "Obtiene la tabla de mejores marcas (PRs) del atleta en distancias estándar de carrera (400m, 800m, 1K, 5K, 10K, " +
        "21K, 42K, etc.) para uno o más períodos de tiempo. Pensado para Run/Swim (ritmo); para Ride no da datos útiles.")]
    public static async Task<string> GetBestEfforts(
        IntervalsIcuClient client,
        [Description("Tipo de deporte, ej. Run, TrailRun, Swim. Por defecto, Run.")] string sportType = "Run",
        [Description("Períodos a comparar, separados por coma: ej. \"42d,1y,all\" (42 días, último año, histórico completo). Por defecto, 42d,1y,all.")]
        string curves = "42d,1y,all",
        CancellationToken ct = default)
    {
        var raw = await client.GetPaceCurvesRawAsync(sportType, curves, ct);
        return FilterToStandardDistances(raw);
    }

    private static string FilterToStandardDistances(string json)
    {
        var node = JsonNode.Parse(json);
        if (node is not JsonObject root || root["list"] is not JsonArray periods)
        {
            return json;
        }

        var referencedActivityIds = new HashSet<string>();

        foreach (var period in periods)
        {
            if (period is not JsonObject periodObj) continue;
            if (periodObj["distance"] is not JsonArray distances || periodObj["values"] is not JsonArray values)
            {
                continue;
            }

            var activityIds = periodObj["activity_id"] as JsonArray;

            var bestEfforts = new JsonArray();
            for (var i = 0; i < distances.Count && i < values.Count; i++)
            {
                if (distances[i] is not JsonValue distanceValue || !distanceValue.TryGetValue<double>(out var distanceMeters))
                {
                    continue;
                }

                if (!StandardRaceDistancesMeters.Any(sd => Math.Abs(sd - distanceMeters) < 0.5))
                {
                    continue;
                }

                string? activityId = null;
                if (activityIds is not null && i < activityIds.Count && activityIds[i] is JsonValue activityIdValue &&
                    activityIdValue.TryGetValue<string>(out var id))
                {
                    activityId = id;
                    referencedActivityIds.Add(id);
                }

                bestEfforts.Add(new JsonObject
                {
                    ["distance_m"] = distanceMeters,
                    ["seconds"] = values[i] is null ? null : JsonNode.Parse(values[i]!.ToJsonString()),
                    ["activity_id"] = activityId,
                });
            }

            periodObj.Remove("distance");
            periodObj.Remove("values");
            periodObj.Remove("activity_id");
            periodObj["best_efforts"] = bestEfforts;
        }

        // El objeto "activities" trae metadata de cada actividad referenciada en la curva
        // completa (cientos de puntos); lo recortamos a solo las que quedaron en la tabla filtrada.
        if (root["activities"] is JsonObject activities)
        {
            foreach (var key in activities.Select(kv => kv.Key).Where(k => !referencedActivityIds.Contains(k)).ToList())
            {
                activities.Remove(key);
            }
        }

        return node.ToJsonString();
    }
}
