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
}
