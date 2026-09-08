using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace MVCCoreApp.Services;

public class CloudinaryService : IFileService
{
    private readonly Cloudinary _cloudinary;
    private readonly ILogger<CloudinaryService> _logger;

    public CloudinaryService(IConfiguration config, ILogger<CloudinaryService> logger)
    {
        _logger = logger;
        var cloudName = config["Cloudinary:CloudName"] ?? Environment.GetEnvironmentVariable("Cloudinary__CloudName") ?? "";
        var apiKey = config["Cloudinary:ApiKey"] ?? Environment.GetEnvironmentVariable("Cloudinary__ApiKey") ?? "";
        var apiSecret = config["Cloudinary:ApiSecret"] ?? Environment.GetEnvironmentVariable("Cloudinary__ApiSecret") ?? "";
        var account = new Account(cloudName, apiKey, apiSecret);
        _cloudinary = new Cloudinary(account);
    }

    public async Task<(string Url, string PublicId)> UploadAsync(IFormFile file, string folder = "elrey")
    {
        if (file == null || file.Length == 0) throw new ArgumentException("Archivo vacío");
        var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowed.Contains(ext)) throw new ArgumentException("Formato no permitido. Usa jpg, png, webp, gif");

        if (file.Length > 10 * 1024 * 1024) throw new ArgumentException("Máx 10MB");

        await using var stream = file.OpenReadStream();
        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = folder,
            PublicId = $"{Path.GetFileNameWithoutExtension(file.FileName)}_{Guid.NewGuid().ToString()[..8]}",
            Overwrite = false,
            Transformation = new Transformation().Quality("auto").FetchFormat("auto")
        };
        var result = await _cloudinary.UploadAsync(uploadParams);
        if (result.Error != null) throw new Exception(result.Error.Message);
        return (result.SecureUrl.ToString(), result.PublicId);
    }

    public async Task<Stream> DownloadAsync(string publicIdOrUrl)
    {
        // Si es URL, extraer publicId o descargar directo
        if (publicIdOrUrl.StartsWith("http"))
        {
            using var http = new HttpClient();
            var bytes = await http.GetByteArrayAsync(publicIdOrUrl);
            return new MemoryStream(bytes);
        }
        // publicId
        var url = _cloudinary.Api.UrlImgUp.BuildUrl(publicIdOrUrl);
        using var http2 = new HttpClient();
        var data = await http2.GetByteArrayAsync(url);
        return new MemoryStream(data);
    }

    public async Task<bool> DeleteAsync(string publicId)
    {
        var del = new DeletionParams(publicId);
        var res = await _cloudinary.DestroyAsync(del);
        return res.Result == "ok";
    }

    public async Task<IEnumerable<FileListItem>> ListAsync(string prefix = "elrey", int max = 100)
    {
        var result = await _cloudinary.ListResourcesByPrefixAsync(prefix, "upload", null);
        if (result.Error != null) throw new Exception(result.Error.Message);
        var resources = result.Resources.Take(max);
        return resources.Select(r => new FileListItem(
            r.SecureUrl?.ToString() ?? r.Url?.ToString() ?? "",
            r.PublicId,
            r.Format,
            r.Bytes,
            DateTime.TryParse(r.CreatedAt, out var dt) ? dt : DateTime.UtcNow,
            r.Width,
            r.Height
        ));
    }
}
