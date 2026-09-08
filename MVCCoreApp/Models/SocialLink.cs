using MongoDB.Bson.Serialization.Attributes;
using MVCCoreApp.Models.Enums;

namespace MVCCoreApp.Models;

public class SocialLink
{
    [BsonElement("platform")]
    public SocialPlatform Platform { get; set; }

    [BsonElement("url")]
    public string Url { get; set; } = string.Empty;

    [BsonElement("iconClass")]
    public string IconClass { get; set; } = string.Empty; // fa-brands fa-spotify

    [BsonElement("showInHero")]
    public bool ShowInHero { get; set; } = true;

    [BsonElement("showInFooter")]
    public bool ShowInFooter { get; set; } = true;

    [BsonElement("order")]
    public int Order { get; set; }

    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;
}
