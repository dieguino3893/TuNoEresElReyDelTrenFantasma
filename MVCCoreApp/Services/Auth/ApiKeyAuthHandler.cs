using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MVCCoreApp.Models;
using MVCCoreApp.Services;

namespace MVCCoreApp.Services.Auth;

public class ApiKeyAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly MongoDbService _mongo;
    public ApiKeyAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, MongoDbService mongo)
        : base(options, logger, encoder) { _mongo = mongo; }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        string? key = null;
        if (Request.Headers.TryGetValue("X-Api-Key", out var v)) key = v.ToString();
        else if (Request.Headers.TryGetValue("Authorization", out var auth) && auth.ToString().StartsWith("ApiKey "))
            key = auth.ToString()["ApiKey ".Length..].Trim();

        if (string.IsNullOrWhiteSpace(key)) return AuthenticateResult.NoResult();

        try
        {
            var hash = ComputeHash(key);
            var col = _mongo.GetCollection<ApiKey>("apiKeys");
            var apiKey = await col.Find(x => x.KeyHash == hash && x.IsActive && !x.IsDeleted && !x.IsRevoked && x.ExpiresAt > DateTime.UtcNow).FirstOrDefaultAsync();
            if (apiKey == null) return AuthenticateResult.Fail("ApiKey inválida o expirada");

            // actualizar lastUsed
            var upd = Builders<ApiKey>.Update.Set(x => x.LastUsedAt, DateTime.UtcNow);
            await col.UpdateOneAsync(x => x.Id == apiKey.Id, upd);

            var claims = new[] {
                new Claim(ClaimTypes.Name, apiKey.Username),
                new Claim(ClaimTypes.Role, apiKey.Role),
                new Claim("UserId", apiKey.UserId),
                new Claim("ApiKeyId", apiKey.Id)
            };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name));
        }
        catch (Exception ex)
        {
            // si Mongo cae no se puede validar la key: NoResult para que lo intenten Basic/Cookie (fail-closed)
            Logger.LogWarning(ex, "ApiKey auth fallo, fallback");
            return AuthenticateResult.NoResult();
        }
    }

    public static string ComputeHash(string key)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(key));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public static string GenerateKey()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return "elrey_" + Convert.ToBase64String(bytes).Replace("+","").Replace("/","").Replace("=","")[..32];
    }
}
