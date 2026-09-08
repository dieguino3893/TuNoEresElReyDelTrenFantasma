namespace MVCCoreApp.Services;

public record FileListItem(string Url, string PublicId, string Format, long Bytes, DateTime CreatedAt, int Width, int Height);

public interface IFileService
{
    Task<(string Url, string PublicId)> UploadAsync(IFormFile file, string folder = "elrey");
    Task<Stream> DownloadAsync(string publicIdOrUrl);
    Task<bool> DeleteAsync(string publicId);
    Task<IEnumerable<FileListItem>> ListAsync(string prefix = "elrey", int max = 100);
}
