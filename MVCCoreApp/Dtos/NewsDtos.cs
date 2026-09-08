namespace MVCCoreApp.Dtos;

public record NewsDto(string Id, string Title, string Slug, string Tag, string ImageUrl, string ImageAlt, string? Excerpt, string ContentHtml, DateTime PublishedAt, bool IsPublished, int Order, DateTime CreatedAt, string CreatedBy, DateTime UpdatedAt, string UpdatedBy);
public record CreateNewsDto(string Title, string Slug, string Tag, string ImageUrl, string ImageAlt, string? Excerpt, string ContentHtml, DateTime PublishedAt, bool IsPublished, int Order);
public record UpdateNewsDto(string Title, string Slug, string Tag, string ImageUrl, string ImageAlt, string? Excerpt, string ContentHtml, DateTime PublishedAt, bool IsPublished, int Order);
