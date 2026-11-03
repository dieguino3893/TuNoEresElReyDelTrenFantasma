using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

namespace MVCCoreApp.Services;

public class CloudinaryService : IFileService
{
    private readonly Cloudinary _cloudinary;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<CloudinaryService> _logger;

    public CloudinaryService(IConfiguration config, IHttpClientFactory httpFactory, ILogger<CloudinaryService> logger)
    {
        _httpFactory = httpFactory;
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
        // Solo se permite descargar del CDN propio: el endpoint público de descarga
        // es anónimo y una URL arbitraria sería un proxy SSRF.
        static string SoloCloudinary(string url, Cloudinary cloudinary)
        {
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            {
                url = cloudinary.Api.UrlImgUp.BuildUrl(url);
            }
            var host = new Uri(url).Host.ToLowerInvariant();
            if (host != "res.cloudinary.com")
            {
                throw new ArgumentException("Solo se pueden descargar archivos del CDN propio.");
            }
            return url;
        }

        var final = SoloCloudinary(publicIdOrUrl, _cloudinary);
        using var http = _httpFactory.CreateClient();
        using var respuesta = await http.GetAsync(final, HttpCompletionOption.ResponseHeadersRead);
        respuesta.EnsureSuccessStatusCode();
        const long maximo = 25 * 1024 * 1024;
        if (respuesta.Content.Headers.ContentLength > maximo)
        {
            throw new ArgumentException("Archivo demasiado grande (máx 25MB).");
        }
        var bytes = await respuesta.Content.ReadAsByteArrayAsync();
        if (bytes.Length > maximo)
        {
            throw new ArgumentException("Archivo demasiado grande (máx 25MB).");
        }
        return new MemoryStream(bytes);
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
