using MongoDB.Bson.Serialization.Attributes;
using MVCCoreApp.Models.Base;

namespace MVCCoreApp.Models;

public class News : AuditableDocument
{
    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("slug")]
    public string Slug { get; set; } = string.Empty;

    [BsonElement("tag")]
    public string Tag { get; set; } = "Música"; // Música | La banda

    [BsonElement("imageUrl")]
    public string ImageUrl { get; set; } = string.Empty;

    [BsonElement("imageAlt")]
    public string ImageAlt { get; set; } = string.Empty;

    [BsonElement("excerpt")]
    public string? Excerpt { get; set; }

    [BsonElement("contentHtml")]
    public string ContentHtml { get; set; } = string.Empty;

    [BsonElement("publishedAt")]
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("isPublished")]
    public bool IsPublished { get; set; } = true;

    [BsonElement("order")]
    public int Order { get; set; }
}
