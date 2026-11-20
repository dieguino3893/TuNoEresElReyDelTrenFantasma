using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MVCCoreApp.Services;

namespace MVCCoreApp.Controllers.Api.V1;

[ApiController]
[Route("api/v1/files")]
public class FilesController : ControllerBase
{
    private readonly IFileService _files;
    private readonly ILogger<FilesController> _log;
    public FilesController(IFileService files, ILogger<FilesController> log) { _files = files; _log = log; }

    // drag & drop upload — cliente → servidor (Cloudinary)
    [HttpPost("upload")]
    [Authorize(AuthenticationSchemes = "Cookies,Basic,ApiKey")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> Upload([FromForm] IFormFile file, [FromQuery] string folder = "elrey")
    {
        if (file == null || file.Length == 0) return BadRequest(new { detail = "Archivo vacío" });
        try
        {
            var (url, publicId) = await _files.UploadAsync(file, folder);
            return Ok(new { url, publicId, fileName = file.FileName, size = file.Length });
        }
        catch (ArgumentException ex) { return BadRequest(new { detail = ex.Message }); }
        catch (Exception ex) { _log.LogWarning(ex, "Upload falló"); return StatusCode(500, new { detail = "No se pudo subir." }); }
    }

    // cliente ← servidor — descarga proxy (por url o publicId)
    [HttpGet("download")]
    [AllowAnonymous]
    public async Task<IActionResult> Download([FromQuery] string url, [FromQuery] string? fileName = null)
    {
        if (string.IsNullOrWhiteSpace(url)) return BadRequest(new { detail = "Falta ?url=" });
        try
        {
            var stream = await _files.DownloadAsync(url);
            var name = fileName ?? Path.GetFileName(new Uri(url).LocalPath);
            if (string.IsNullOrWhiteSpace(name)) name = "download.jpg";
            return File(stream, "application/octet-stream", name);
        }
        catch (Exception ex) { _log.LogWarning(ex, "Descarga falló"); return StatusCode(500, new { detail = "No se pudo descargar." }); }
    }

    // descarga por publicId (ej: elrey/foto_abc123)
    [HttpGet("download/{*publicId}")]
    [AllowAnonymous]
    public async Task<IActionResult> DownloadById(string publicId)
    {
        try
        {
            var stream = await _files.DownloadAsync(publicId);
            var name = publicId.Split('/').Last() + ".jpg";
            return File(stream, "application/octet-stream", name);
        }
        catch (Exception ex) { _log.LogWarning(ex, "Descarga falló"); return StatusCode(500, new { detail = "No se pudo descargar." }); }
    }

    [HttpGet]
    [Authorize(AuthenticationSchemes = "Cookies,Basic,ApiKey")]
    public async Task<IActionResult> List([FromQuery] string prefix = "elrey", [FromQuery] int max = 100)
    {
        try
        {
            var items = await _files.ListAsync(prefix, Math.Min(max, 100));
            return Ok(items);
        }
        catch (Exception ex) { _log.LogWarning(ex, "Lista falló"); return StatusCode(500, new { detail = "No se pudo listar." }); }
    }

    // shortcut: el publicId viaja por query (?publicId=) porque en ruta el %2F
    // de la carpeta llegaba sin decodificar y Cloudinary respondía "not found".
    [HttpDelete]
    [Authorize(AuthenticationSchemes = "Cookies,Basic,ApiKey")]
    public async Task<IActionResult> Delete([FromQuery] string publicId)
    {
        if (string.IsNullOrWhiteSpace(publicId)) return BadRequest(new { detail = "Falta ?publicId=" });
        try
        {
            var resultado = await _files.DeleteAsync(publicId);
            if (resultado == "ok") return Ok(new { message = "Eliminado" });
            _log.LogWarning("Borrado Cloudinary devolvió {Resultado} para {PublicId}", resultado, publicId);
            return NotFound(new { detail = $"Cloudinary dice: {resultado}" });
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Borrado falló para {PublicId}", publicId);
            return StatusCode(500, new { detail = ex.Message });
        }
    }
}
