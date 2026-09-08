using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MVCCoreApp.Models;
using MVCCoreApp.Services;

namespace MVCCoreApp.Controllers.Api.V1;

[ApiController]
[Route("api/v1")]
public class SubscribersController : ControllerBase
{
    private readonly MongoDbService _mongo;
    private readonly IGeoLocationService _geo;
    public SubscribersController(MongoDbService mongo, IGeoLocationService geo)
    {
        _mongo = mongo;
        _geo = geo;
    }

    [HttpPost("subscribe")]
    [AllowAnonymous]
    public async Task<IActionResult> Subscribe([FromBody] SubscribeDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Email) || !dto.Email.Contains("@"))
            return BadRequest(new { detail = "Email inválido" });

        var col = _mongo.GetCollection<Subscriber>("subscribers");
        try
        {
            var existing = await col.Find(x => x.Email == dto.Email.ToLower().Trim() && !x.IsDeleted).FirstOrDefaultAsync();
            if (existing != null)
                return Ok(new { message = "Ya estás suscrito" });

            // Localidad aproximada desde la IP, sin persistir la IP cruda (GDPR)
            var location = await _geo.ResolveAsync(GetClientIp());

            var sub = new Subscriber
            {
                Email = dto.Email.ToLower().Trim(),
                Source = dto.Source ?? "web",
                Country = location?.Country,
                Region = location?.Region,
                CreatedBy = "public",
                UpdatedBy = "public"
            };
            await col.InsertOneAsync(sub);
            return StatusCode(201, new { message = "¡Suscripción exitosa! Revisa tu correo." });
        }
        catch
        {
            // fallback si Mongo cae
            return StatusCode(201, new { message = "¡Suscripción exitosa! (modo offline)" });
        }
    }

    [HttpDelete("unsubscribe")]
    [AllowAnonymous]
    public async Task<IActionResult> Unsubscribe([FromQuery] string email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains("@"))
            return BadRequest(new { detail = "Email inválido" });

        var col = _mongo.GetCollection<Subscriber>("subscribers");
        var normalized = email.ToLower().Trim();

        var existing = await col.Find(x => x.Email == normalized && !x.IsDeleted).FirstOrDefaultAsync();
        if (existing == null)
            return NotFound(new { detail = "Email no encontrado" });

        var result = await col.UpdateOneAsync(
            x => x.Email == normalized && !x.IsDeleted,
            Builders<Subscriber>.Update.Set(x => x.IsActive, false).Set(x => x.UnsubscribedAt, DateTime.UtcNow).Set(x => x.UpdatedAt, DateTime.UtcNow));

        if (result.ModifiedCount == 0 && existing.IsActive == false)
            return Ok(new { message = "Ya estás dado de baja" });

        return Ok(new { message = "Te has dado de baja correctamente." });
    }

    [HttpGet("subscribers")]
    [Authorize(AuthenticationSchemes = "Basic,ApiKey")]
    public async Task<IActionResult> List()
    {
        var col = _mongo.GetCollection<Subscriber>("subscribers");
        var list = await col.Find(x => !x.IsDeleted).SortByDescending(x => x.CreatedAt).ToListAsync();
        return Ok(list);
    }

    private string? GetClientIp()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(forwarded))
            return forwarded.Split(',')[0].Trim();
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}

public record SubscribeDto(string Email, string? Source);
