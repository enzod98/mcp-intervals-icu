using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IntervalsMcp.Auth;

/// <summary>Secreto compartido necesario para acceder a este servidor (se define con MCP_AUTH_TOKEN).</summary>
public record McpAuthOptions(string Token);

/// <summary>
/// Valida un único token Bearer fijo contra el header "Authorization".
/// Es intencionalmente simple (sin emisor OAuth/JWT): el servidor tiene un solo
/// llamador legítimo (el propio cliente Claude del dueño), así que un secreto compartido alcanza.
/// </summary>
public class StaticBearerTokenHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    McpAuthOptions authOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "StaticBearer";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var headerValue))
        {
            return Task.FromResult(AuthenticateResult.Fail("Falta el header Authorization."));
        }

        var header = headerValue.ToString();
        const string prefix = "Bearer ";
        if (!header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.Fail("El header Authorization debe usar el esquema Bearer."));
        }

        var providedToken = header[prefix.Length..].Trim();
        if (!TokensMatch(providedToken, authOptions.Token))
        {
            return Task.FromResult(AuthenticateResult.Fail("Token inválido."));
        }

        var identity = new ClaimsIdentity(SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    // Comparación en tiempo constante para que el tiempo de respuesta no permita adivinar el token byte a byte.
    private static bool TokensMatch(string provided, string expected)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        return providedBytes.Length == expectedBytes.Length &&
               CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}
