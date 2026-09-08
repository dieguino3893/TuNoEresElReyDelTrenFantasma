namespace MVCCoreApp.Dtos;

public record MediaDto(string Id, string Name, string Url, string PublicId, string Folder, string? Format, long Bytes, DateTime CreatedAt, string CreatedBy);
public record CreateMediaDto(string Name, string Folder);
