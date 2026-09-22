using System.ComponentModel;
using ModelContextProtocol.Server;

namespace IntervalsMcp.Tools;

[McpServerToolType]
public static class AthleteTools
{
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
}
