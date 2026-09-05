using System.Net.Http.Headers;
using System.Text;
using IntervalsMcp;
using IntervalsMcp.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var apiKey = Environment.GetEnvironmentVariable("INTERVALS_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.Error.WriteLine(
        "ERROR: la variable de entorno INTERVALS_API_KEY no esta definida. " +
        "Genera una API key en https://intervals.icu/settings y expórtala antes de iniciar el servidor.");
    return 1;
}

var athleteId = Environment.GetEnvironmentVariable("INTERVALS_ATHLETE_ID");
if (string.IsNullOrWhiteSpace(athleteId))
{
    athleteId = "0"; // "0" hace referencia al atleta autenticado por la API key.
}

var transport = Environment.GetEnvironmentVariable("MCP_TRANSPORT")?.Trim().ToLowerInvariant();

if (transport == "http")
{
    var authToken = Environment.GetEnvironmentVariable("MCP_AUTH_TOKEN");
    if (string.IsNullOrWhiteSpace(authToken))
    {
        Console.Error.WriteLine(
            "ERROR: MCP_TRANSPORT=http requiere MCP_AUTH_TOKEN definido: es el token Bearer que tu cliente " +
            "MCP debe presentar para poder usar este servidor. Generá uno largo y aleatorio, por ejemplo con " +
            "'openssl rand -hex 32'.");
        return 1;
    }

    await RunHttpAsync(args, apiKey, athleteId, authToken);
}
else
{
    await RunStdioAsync(args, apiKey, athleteId);
}

return 0;

static async Task RunStdioAsync(string[] args, string apiKey, string athleteId)
{
    var builder = Host.CreateApplicationBuilder(args);

    // stdout está reservado para los mensajes del protocolo MCP, así que todos los logs van a stderr.
    builder.Logging.AddConsole(options =>
    {
        options.LogToStandardErrorThreshold = LogLevel.Trace;
    });

    ConfigureIntervalsServices(builder.Services, apiKey, athleteId);

    builder.Services
        .AddMcpServer()
        .WithStdioServerTransport()
        .WithToolsFromAssembly();

    await builder.Build().RunAsync();
}

static async Task RunHttpAsync(string[] args, string apiKey, string athleteId, string authToken)
{
    var builder = WebApplication.CreateBuilder(args);

    ConfigureIntervalsServices(builder.Services, apiKey, athleteId);

    builder.Services.AddSingleton(new McpAuthOptions(authToken));
    builder.Services
        .AddAuthentication(StaticBearerTokenHandler.SchemeName)
        .AddScheme<AuthenticationSchemeOptions, StaticBearerTokenHandler>(StaticBearerTokenHandler.SchemeName, _ => { });
    builder.Services.AddAuthorization();

    builder.Services
        .AddMcpServer()
        .WithHttpTransport()
        .WithToolsFromAssembly();

    var app = builder.Build();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapMcp("/mcp").RequireAuthorization();

    await app.RunAsync();
}

static void ConfigureIntervalsServices(IServiceCollection services, string apiKey, string athleteId)
{
    services.AddSingleton(new IntervalsIcuOptions(athleteId));

    services.AddHttpClient<IntervalsIcuClient>(client =>
    {
        client.BaseAddress = new Uri("https://intervals.icu/api/v1/");
        var authBytes = Encoding.ASCII.GetBytes($"API_KEY:{apiKey}");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
    });
}
