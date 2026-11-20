using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace MVCCoreApp.Services.Auth;

/// <summary>
/// Acceso de ensayo por link compartido: ?key=... o header X-Share-Key.
/// Solo coincide con Playback:ShareKey; sin llave configurada no autentica nada.
/// </summary>
public class ShareKeyAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IConfiguration _config;

    public ShareKeyAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration config)
        : base(options, logger, encoder) { _config = config; }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var esperada = _config["Playback:ShareKey"];
        if (string.IsNullOrWhiteSpace(esperada)) return Task.FromResult(AuthenticateResult.NoResult());

        string? recibida = null;
        if (Request.Query.TryGetValue("key", out var q)) recibida = q.ToString();
        if (string.IsNullOrWhiteSpace(recibida) && Request.Headers.TryGetValue("X-Share-Key", out var h))
            recibida = h.ToString();

        if (string.IsNullOrWhiteSpace(recibida)) return Task.FromResult(AuthenticateResult.NoResult());
        if (!CryptographicEquals(recibida.Trim(), esperada.Trim()))
            return Task.FromResult(AuthenticateResult.Fail("Llave de ensayo inválida"));

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "ensayo"),
            new Claim(ClaimTypes.Role, "Ensayo")
        }, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }

    private static bool CryptographicEquals(string a, string b)
    {
        if (a.Length != b.Length) return false;
        var diff = 0;
        for (var i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
        return diff == 0;
    }
}
