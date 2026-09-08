using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MVCCoreApp.Dtos;
using MVCCoreApp.Models;
using MVCCoreApp.Services;

namespace MVCCoreApp.Controllers.Api.V1;

[ApiController]
[Route("api/v1/tracks")]
public class TracksController : ControllerBase
{
    private readonly MongoDbService _mongo;
    public TracksController(MongoDbService mongo) => _mongo = mongo;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<TrackDto>>> GetAll()
    {
        var col = _mongo.GetCollection<Track>("tracks");
        var list = await col.Find(x => !x.IsDeleted && x.IsVisible).SortBy(x => x.Order).ThenBy(x => x.TrackNumber).ToListAsync();
        return Ok(list.Select(Map));
    }

    [HttpGet("admin")]
    [Authorize(AuthenticationSchemes = "Basic,ApiKey")]
    public async Task<ActionResult<IEnumerable<TrackDto>>> GetAllAdmin()
    {
        var col = _mongo.GetCollection<Track>("tracks");
        var list = await col.Find(x => !x.IsDeleted).SortBy(x => x.Order).ToListAsync();
        return Ok(list.Select(Map));
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<TrackDto>> GetById(string id)
    {
        var col = _mongo.GetCollection<Track>("tracks");
        var t = await col.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (t == null) return NotFound();
        return Ok(Map(t));
    }

    [HttpPost]
    [Authorize(AuthenticationSchemes = "Basic,ApiKey")]
    public async Task<ActionResult<TrackDto>> Create([FromBody] CreateTrackDto dto)
    {
        var col = _mongo.GetCollection<Track>("tracks");
        var user = User.Identity?.Name ?? "system";
        var t = new Track
        {
            Title = dto.Title, TrackNumber = dto.TrackNumber, Type = dto.Type,
            Links = dto.Links, ExternalId = dto.ExternalId, Source = "Manual",
            IsVisible = dto.IsVisible, Order = dto.Order,
            CreatedBy = user, UpdatedBy = user
        };
        if (!string.IsNullOrEmpty(t.Links.SpotifyTrackId) && string.IsNullOrEmpty(t.Links.SpotifyEmbedUrl))
            t.Links.SpotifyEmbedUrl = $"https://open.spotify.com/embed/track/{t.Links.SpotifyTrackId}?utm_source=generator&theme=0";

        await col.InsertOneAsync(t);
        return CreatedAtAction(nameof(GetById), new { id = t.Id }, Map(t));
    }

    [HttpPut("{id}")]
    [Authorize(AuthenticationSchemes = "Basic,ApiKey")]
    public async Task<ActionResult<TrackDto>> Update(string id, [FromBody] UpdateTrackDto dto)
    {
        var col = _mongo.GetCollection<Track>("tracks");
        var t = await col.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (t == null) return NotFound();
        t.Title = dto.Title; t.TrackNumber = dto.TrackNumber; t.Type = dto.Type; t.Links = dto.Links; t.ExternalId = dto.ExternalId; t.IsVisible = dto.IsVisible; t.Order = dto.Order;
        if (!string.IsNullOrEmpty(t.Links.SpotifyTrackId) && string.IsNullOrEmpty(t.Links.SpotifyEmbedUrl))
            t.Links.SpotifyEmbedUrl = $"https://open.spotify.com/embed/track/{t.Links.SpotifyTrackId}?utm_source=generator&theme=0";
        t.UpdatedAt = DateTime.UtcNow; t.UpdatedBy = User.Identity?.Name ?? "system";
        await col.ReplaceOneAsync(x => x.Id == id, t);
        return Ok(Map(t));
    }

    [HttpDelete("{id}")]
    [Authorize(AuthenticationSchemes = "Basic,ApiKey")]
    public async Task<IActionResult> Delete(string id)
    {
        var col = _mongo.GetCollection<Track>("tracks");
        var t = await col.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (t == null) return NotFound();
        t.IsDeleted = true; t.UpdatedAt = DateTime.UtcNow; t.UpdatedBy = User.Identity?.Name ?? "system";
        await col.ReplaceOneAsync(x => x.Id == id, t);
        return NoContent();
    }

    private static TrackDto Map(Track t) => new(t.Id, t.Title, t.TrackNumber, t.Type, t.Links, t.Source, t.ExternalId, t.IsVisible, t.Order, t.CreatedAt, t.CreatedBy, t.UpdatedAt, t.UpdatedBy);
}
