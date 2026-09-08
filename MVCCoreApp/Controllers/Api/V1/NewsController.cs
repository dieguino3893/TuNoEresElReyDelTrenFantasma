using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MVCCoreApp.Dtos;
using MVCCoreApp.Models;
using MVCCoreApp.Services;

namespace MVCCoreApp.Controllers.Api.V1;

[ApiController]
[Route("api/v1/news")]
public class NewsController : ControllerBase
{
    private readonly MongoDbService _mongo;
    public NewsController(MongoDbService mongo) => _mongo = mongo;

    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<NewsDto>>> GetAll([FromQuery] bool? publishedOnly = true)
    {
        var col = _mongo.GetCollection<News>("news");
        var filter = Builders<News>.Filter.Eq(x => x.IsDeleted, false);
        if (publishedOnly == true) filter &= Builders<News>.Filter.Eq(x => x.IsPublished, true);
        var list = await col.Find(filter).SortBy(x => x.Order).ThenByDescending(x => x.PublishedAt).ToListAsync();
        return Ok(list.Select(Map));
    }

    [HttpGet("admin")]
    [Authorize(AuthenticationSchemes = "Basic,ApiKey")]
    public async Task<ActionResult<IEnumerable<NewsDto>>> GetAllAdmin()
    {
        var col = _mongo.GetCollection<News>("news");
        var list = await col.Find(x => !x.IsDeleted).SortBy(x => x.Order).ToListAsync();
        return Ok(list.Select(Map));
    }

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<ActionResult<NewsDto>> GetById(string id)
    {
        var col = _mongo.GetCollection<News>("news");
        var n = await col.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (n == null) return NotFound();
        return Ok(Map(n));
    }

    [HttpGet("slug/{slug}")]
    [AllowAnonymous]
    public async Task<ActionResult<NewsDto>> GetBySlug(string slug)
    {
        var col = _mongo.GetCollection<News>("news");
        var n = await col.Find(x => x.Slug == slug && !x.IsDeleted).FirstOrDefaultAsync();
        if (n == null) return NotFound();
        return Ok(Map(n));
    }

    [HttpPost]
    [Authorize(AuthenticationSchemes = "Basic,ApiKey")]
    public async Task<ActionResult<NewsDto>> Create([FromBody] CreateNewsDto dto)
    {
        var col = _mongo.GetCollection<News>("news");
        if (await col.Find(x => x.Slug == dto.Slug && !x.IsDeleted).AnyAsync())
            return Conflict(new { message = $"Slug '{dto.Slug}' ya existe" });

        var user = User.Identity?.Name ?? "system";
        var n = new News { Title = SanitizeService.Sanitize(dto.Title), Slug = dto.Slug, Tag = SanitizeService.SanitizeText(dto.Tag), ImageUrl = dto.ImageUrl, ImageAlt = SanitizeService.Sanitize(dto.ImageAlt), Excerpt = SanitizeService.Sanitize(dto.Excerpt), ContentHtml = SanitizeService.Sanitize(dto.ContentHtml), PublishedAt = dto.PublishedAt, IsPublished = dto.IsPublished, Order = dto.Order, CreatedBy = user, UpdatedBy = user };
        await col.InsertOneAsync(n);
        return CreatedAtAction(nameof(GetById), new { id = n.Id }, Map(n));
    }

    [HttpPut("{id}")]
    [Authorize(AuthenticationSchemes = "Basic,ApiKey")]
    public async Task<ActionResult<NewsDto>> Update(string id, [FromBody] UpdateNewsDto dto)
    {
        var col = _mongo.GetCollection<News>("news");
        var n = await col.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (n == null) return NotFound();

        if (n.Slug != dto.Slug && await col.Find(x => x.Slug == dto.Slug && !x.IsDeleted).AnyAsync())
            return Conflict(new { message = $"Slug '{dto.Slug}' ya existe" });

        n.Title = SanitizeService.Sanitize(dto.Title); n.Slug = dto.Slug; n.Tag = SanitizeService.SanitizeText(dto.Tag); n.ImageUrl = dto.ImageUrl; n.ImageAlt = SanitizeService.Sanitize(dto.ImageAlt); n.Excerpt = SanitizeService.Sanitize(dto.Excerpt); n.ContentHtml = SanitizeService.Sanitize(dto.ContentHtml); n.PublishedAt = dto.PublishedAt; n.IsPublished = dto.IsPublished; n.Order = dto.Order;
        n.UpdatedAt = DateTime.UtcNow; n.UpdatedBy = User.Identity?.Name ?? "system";
        await col.ReplaceOneAsync(x => x.Id == id, n);
        return Ok(Map(n));
    }

    [HttpDelete("{id}")]
    [Authorize(AuthenticationSchemes = "Basic,ApiKey")]
    public async Task<IActionResult> Delete(string id)
    {
        var col = _mongo.GetCollection<News>("news");
        var n = await col.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (n == null) return NotFound();
        n.IsDeleted = true; n.UpdatedAt = DateTime.UtcNow; n.UpdatedBy = User.Identity?.Name ?? "system";
        await col.ReplaceOneAsync(x => x.Id == id, n);
        return NoContent();
    }

    private static NewsDto Map(News n) => new(n.Id, n.Title, n.Slug, n.Tag, n.ImageUrl, n.ImageAlt, n.Excerpt, n.ContentHtml, n.PublishedAt, n.IsPublished, n.Order, n.CreatedAt, n.CreatedBy, n.UpdatedAt, n.UpdatedBy);
}
