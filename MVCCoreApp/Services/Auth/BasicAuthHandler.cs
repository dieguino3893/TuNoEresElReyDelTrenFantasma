using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MVCCoreApp.Models;

namespace MVCCoreApp.Services.Auth;

public class BasicAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly MongoDbService _mongo;

    public BasicAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        MongoDbService mongo)
        : base(options, logger, encoder)
    {
        _mongo = mongo;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.ContainsKey("Authorization"))
            return AuthenticateResult.NoResult();

        try
        {
            var header = AuthenticationHeaderValue.Parse(Request.Headers["Authorization"]!);
            if (header.Scheme != "Basic") return AuthenticateResult.NoResult();

            var bytes = Convert.FromBase64String(header.Parameter!);
            var credentials = Encoding.UTF8.GetString(bytes).Split(':', 2);
            if (credentials.Length != 2) return AuthenticateResult.Fail("Invalid Basic format");

            var username = credentials[0];
            var password = credentials[1];

            User? user = null;
            var col = _mongo.GetCollection<User>("users");
            user = await col.Find(u => u.Username == username && !u.IsDeleted && u.IsActive).FirstOrDefaultAsync();
            if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
                return AuthenticateResult.Fail("Invalid username or password");

            var claims = new[]
            {
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("UserId", user.Id),
            };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            return AuthenticateResult.Fail(ex.Message);
        }
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers["WWW-Authenticate"] = "Basic realm=\"ElReyDelTrenFantasma Admin\"";
        return base.HandleChallengeAsync(properties);
    }
}
