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
        var images = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        var audios = new[] { ".mp3", ".m4a", ".wav", ".ogg", ".flac" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var esAudio = audios.Contains(ext);
        if (!images.Contains(ext) && !esAudio)
            throw new ArgumentException("Formato no permitido. Usa jpg, png, webp, gif, mp3, m4a, wav, ogg o flac");

        var maximo = esAudio ? 25L * 1024 * 1024 : 10L * 1024 * 1024;
        if (file.Length > maximo) throw new ArgumentException(esAudio ? "Máx 25MB" : "Máx 10MB");

        await using var stream = file.OpenReadStream();
        var publicId = $"{Path.GetFileNameWithoutExtension(file.FileName)}_{Guid.NewGuid().ToString()[..8]}";

        if (esAudio)
        {
            var audioParams = new VideoUploadParams
            {
                File = new FileDescription(file.FileName, stream),
                Folder = folder,
                PublicId = publicId,
                Overwrite = false
            };
            var audioResult = await _cloudinary.UploadAsync(audioParams);
            if (audioResult.Error != null) throw new Exception(audioResult.Error.Message);
            return (audioResult.SecureUrl.ToString(), audioResult.PublicId);
        }

        var uploadParams = new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = folder,
            PublicId = publicId,
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

    public async Task<string> DeleteAsync(string publicId)
    {
        var del = new DeletionParams(publicId);
        var res = await _cloudinary.DestroyAsync(del);
        if (res.Error != null) throw new Exception(res.Error.Message);
        // shortcut: se devuelve el resultado crudo ("ok", "not found"...) para diagnosticar.
        return res.Result ?? "desconocido";
    }

    public async Task<IEnumerable<FileListItem>> ListAsync(string prefix = "elrey", int max = 100)
    {
        // shortcut: la API pagina de 10 en 10; se recorre el cursor hasta juntar el maximo.
        var items = new List<FileListItem>();
        string? cursor = null;
        do
        {
            var result = await _cloudinary.ListResourcesByPrefixAsync(prefix, "upload", cursor);
            if (result.Error != null) throw new Exception(result.Error.Message);
            items.AddRange(result.Resources.Select(r => new FileListItem(
                r.SecureUrl?.ToString() ?? r.Url?.ToString() ?? "",
                r.PublicId,
                r.Format,
                r.Bytes,
                DateTime.TryParse(r.CreatedAt, out var dt) ? dt : DateTime.UtcNow,
                r.Width,
                r.Height
            )));
            cursor = result.NextCursor;
        } while (!string.IsNullOrEmpty(cursor) && items.Count < max);
        return items.Take(Math.Min(max, 500));
    }
}
