using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MVCCoreApp.Models;
using MVCCoreApp.Services;
using MVCCoreApp.Services.Auth;

namespace MVCCoreApp.Controllers.Api.V1;

[ApiController]
[Route("api/v1/auth/api-keys")]
public class ApiKeysController : ControllerBase
{
    private readonly MongoDbService _mongo;
    public ApiKeysController(MongoDbService mongo) => _mongo = mongo;

    // crear api-key para la sesión actual (requiere estar logueado por Cookie o Basic)
    [HttpPost]
    [Authorize(AuthenticationSchemes = "Cookies,Basic,ApiKey")]
    public async Task<IActionResult> Create([FromQuery] int hours = 8)
    {
        var username = User.Identity?.Name ?? "system";
        var userId = User.FindFirst("UserId")?.Value ?? username;
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;
        var plain = ApiKeyAuthHandler.GenerateKey();
        var hash = ApiKeyAuthHandler.ComputeHash(plain);
        var apiKey = new ApiKey
        {
            KeyHash = hash,
            Prefix = plain[..8],
            Username = username,
            UserId = userId,
            Role = role,
            SessionId = HttpContext.TraceIdentifier,
            ExpiresAt = DateTime.UtcNow.AddHours(Math.Clamp(hours, 1, 72)),
            CreatedBy = username,
            UpdatedBy = username
        };
        try
        {
            await _mongo.GetCollection<ApiKey>("apiKeys").InsertOneAsync(apiKey);
        }
        catch (Exception ex)
        {
            // si Mongo cae, devolver la key igual pero sin persistir (solo demo)
            return Ok(new { apiKey = plain, prefix = apiKey.Prefix, expiresAt = apiKey.ExpiresAt, warning = "Mongo no disponible, key no persistida: " + ex.Message });
        }
        // solo se muestra una vez
        return Ok(new { apiKey = plain, prefix = apiKey.Prefix, expiresAt = apiKey.ExpiresAt, id = apiKey.Id });
    }

    [HttpGet]
    [Authorize(AuthenticationSchemes = "Cookies,Basic,ApiKey")]
    public async Task<IActionResult> List()
    {
        var username = User.Identity?.Name;
        var col = _mongo.GetCollection<ApiKey>("apiKeys");
        var list = await col.Find(x => x.Username == username && !x.IsDeleted).SortByDescending(x => x.CreatedAt).ToListAsync();
        return Ok(list.Select(x => new { x.Id, x.Prefix, x.Username, x.Role, x.ExpiresAt, x.LastUsedAt, x.IsActive, x.IsRevoked, x.CreatedAt }));
    }

    [HttpDelete("{id}")]
    [Authorize(AuthenticationSchemes = "Cookies,Basic,ApiKey")]
    public async Task<IActionResult> Revoke(string id)
    {
        var username = User.Identity?.Name;
        var col = _mongo.GetCollection<ApiKey>("apiKeys");
        var k = await col.Find(x => x.Id == id && x.Username == username).FirstOrDefaultAsync();
        if (k == null) return NotFound();
        k.IsRevoked = true; k.IsActive = false; k.UpdatedAt = DateTime.UtcNow; k.UpdatedBy = username ?? "system";
        await col.ReplaceOneAsync(x => x.Id == id, k);
        return Ok(new { message = "Revocada" });
    }

    // validar que la key actual funciona
    [HttpGet("me")]
    [Authorize(AuthenticationSchemes = "ApiKey")]
    public IActionResult Me() => Ok(new { user = User.Identity?.Name, role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value });
}
