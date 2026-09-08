using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using MVCCoreApp.Dtos;
using MVCCoreApp.Models;
using MVCCoreApp.Services;

namespace MVCCoreApp.Controllers.Api.V1;

[ApiController]
[Route("api/v1/media")]
public class MediaController : ControllerBase
{
    private readonly MongoDbService _mongo;
    private readonly IFileService _files;
    public MediaController(MongoDbService mongo, IFileService files) { _mongo = mongo; _files = files; }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> List()
    {
        try
        {
            var col = _mongo.GetCollection<Media>("media");
            var list = await col.Find(x => !x.IsDeleted).SortByDescending(x => x.CreatedAt).ToListAsync();
            return Ok(list.Select(m => new MediaDto(m.Id, m.Name, m.Url, m.PublicId, m.Folder, m.Format, m.Bytes, m.CreatedAt, m.CreatedBy)));
        }
        catch { return Ok(Array.Empty<MediaDto>()); }
    }

    [HttpPost]
    [Authorize(AuthenticationSchemes = "Cookies,Basic,ApiKey")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> Create([FromForm] IFormFile file, [FromForm] string name, [FromForm] string folder = "elrey")
    {
        if (file == null || file.Length == 0) return BadRequest(new { detail = "Archivo vacío" });
        if (string.IsNullOrWhiteSpace(name)) name = Path.GetFileNameWithoutExtension(file.FileName);
        try
        {
            var (url, publicId) = await _files.UploadAsync(file, folder);
            // guardar en Media con nombre
            var media = new Media
            {
                Name = name.Trim(),
                Url = url,
                PublicId = publicId,
                Folder = folder,
                Format = Path.GetExtension(file.FileName).TrimStart('.').ToLower(),
                Bytes = file.Length,
                CreatedBy = User.Identity?.Name ?? "system",
                UpdatedBy = User.Identity?.Name ?? "system"
            };
            try { await _mongo.GetCollection<Media>("media").InsertOneAsync(media); } catch { /* fallback si Mongo cae, igual devolvemos url */ }
            return Ok(new MediaDto(media.Id, media.Name, media.Url, media.PublicId, media.Folder, media.Format, media.Bytes, media.CreatedAt, media.CreatedBy));
        }
        catch (ArgumentException ex) { return BadRequest(new { detail = ex.Message }); }
        catch (Exception ex) { return StatusCode(500, new { detail = ex.Message }); }
    }

    [HttpDelete("{id}")]
    [Authorize(AuthenticationSchemes = "Cookies,Basic,ApiKey")]
    public async Task<IActionResult> Delete(string id)
    {
        var col = _mongo.GetCollection<Media>("media");
        var m = await col.Find(x => x.Id == id && !x.IsDeleted).FirstOrDefaultAsync();
        if (m == null) return NotFound();
        try { await _files.DeleteAsync(m.PublicId); } catch { }
        m.IsDeleted = true; m.UpdatedAt = DateTime.UtcNow; m.UpdatedBy = User.Identity?.Name ?? "system";
        await col.ReplaceOneAsync(x => x.Id == id, m);
        return Ok(new { message = "Eliminado" });
    }
}
