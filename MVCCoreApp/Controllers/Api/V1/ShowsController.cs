using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MVCCoreApp.Dtos;
using MVCCoreApp.Models;
using MVCCoreApp.Services;

namespace MVCCoreApp.Controllers.Api.V1;

[ApiController]
[Route("api/v1/shows")]
public class ShowsController : ControllerBase
{
    private readonly MongoDbService _mongo;
    public ShowsController(MongoDbService mongo) => _mongo = mongo;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<ShowDto>>> GetAll()
    {
        var col = _mongo.GetCollection<Show>("shows");
        var list = await col.Find(x => !x.IsDeleted && x.IsVisible).SortBy(x => x.Date).ToListAsync();
        return Ok(list.Select(Map));
    }

    [HttpGet("next")]
    [AllowAnonymous]
    public async Task<ActionResult<ShowDto>> GetNext()
    {
        var col = _mongo.GetCollection<Show>("shows");
        var now = DateTime.UtcNow;
        var next = await col.Find(x => !x.IsDeleted && x.IsVisible && x.Date >= now).SortBy(x => x.Date).FirstOrDefaultAsync();
        if (next == null) return NotFound(new { message = "No upcoming shows" });
        return Ok(Map(next));
    }

    [HttpGet("admin")]
    [Authorize(AuthenticationSchemes = "Basic,ApiKey")]
    public async Task<ActionResult<IEnumerable<ShowDto>>> GetAllAdmin()
    {
        var col = _mongo.GetCollection<Show>("shows");
        var list = await col.Find(x => !x.IsDeleted).SortBy(x => x.Date).ToListAsync();
        return Ok(list.Select(Map));
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<ShowDto>> GetById(string id)
    {
        var col = _mongo.GetCollection<Show>("shows");
        var s = await col.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (s == null) return NotFound();
        return Ok(Map(s));
    }

    [HttpPost]
    [Authorize(AuthenticationSchemes = "Basic,ApiKey")]
    public async Task<ActionResult<ShowDto>> Create([FromBody] CreateShowDto dto)
    {
        var col = _mongo.GetCollection<Show>("shows");
        var user = User.Identity?.Name ?? "system";
        var s = new Show { Date = dto.Date, Time = dto.Time, Venue = dto.Venue, CityState = dto.CityState, GoogleMapsUrl = dto.GoogleMapsUrl, IsVisible = dto.IsVisible, Order = dto.Order, CreatedBy = user, UpdatedBy = user };
        await col.InsertOneAsync(s);
        return CreatedAtAction(nameof(GetById), new { id = s.Id }, Map(s));
    }

    [HttpPut("{id}")]
    [Authorize(AuthenticationSchemes = "Basic,ApiKey")]
    public async Task<ActionResult<ShowDto>> Update(string id, [FromBody] UpdateShowDto dto)
    {
        var col = _mongo.GetCollection<Show>("shows");
        var s = await col.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (s == null) return NotFound();
        s.Date = dto.Date; s.Time = dto.Time; s.Venue = dto.Venue; s.CityState = dto.CityState; s.GoogleMapsUrl = dto.GoogleMapsUrl; s.IsVisible = dto.IsVisible; s.Order = dto.Order;
        s.UpdatedAt = DateTime.UtcNow; s.UpdatedBy = User.Identity?.Name ?? "system";
        await col.ReplaceOneAsync(x => x.Id == id, s);
        return Ok(Map(s));
    }

    [HttpDelete("{id}")]
    [Authorize(AuthenticationSchemes = "Basic,ApiKey")]
    public async Task<IActionResult> Delete(string id)
    {
        var col = _mongo.GetCollection<Show>("shows");
        var s = await col.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (s == null) return NotFound();
        s.IsDeleted = true; s.UpdatedAt = DateTime.UtcNow; s.UpdatedBy = User.Identity?.Name ?? "system";
        await col.ReplaceOneAsync(x => x.Id == id, s);
        return NoContent();
    }

    private static ShowDto Map(Show s) => new(s.Id, s.Date, s.Time, s.Venue, s.CityState, s.GoogleMapsUrl, s.IsVisible, s.Order, s.CreatedAt, s.CreatedBy, s.UpdatedAt, s.UpdatedBy);
}
