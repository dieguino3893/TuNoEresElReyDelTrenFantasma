using MongoDB.Bson.Serialization.Attributes;

namespace MVCCoreApp.Models;

public class SiteSettings
{
    [BsonId]
    public string Id { get; set; } = "main";

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("createdBy")]
    public string CreatedBy { get; set; } = "system";

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedBy")]
    public string UpdatedBy { get; set; } = "system";

    [BsonElement("isDeleted")]
    public bool IsDeleted { get; set; } = false;
    [BsonElement("siteTitle")]
    public string SiteTitle { get; set; } = string.Empty;

    [BsonElement("slogan")]
    public string Slogan { get; set; } = string.Empty;

    [BsonElement("heroEyebrow")]
    public string HeroEyebrow { get; set; } = string.Empty;

    [BsonElement("heroLocation")]
    public string HeroLocation { get; set; } = string.Empty;

    [BsonElement("heroImageUrl")]
    public string HeroImageUrl { get; set; } = string.Empty;

    [BsonElement("bandTitle")]
    public string BandTitle { get; set; } = string.Empty;

    [BsonElement("bandHighlight")]
    public string BandHighlight { get; set; } = string.Empty;

    [BsonElement("bandHighlightSub")]
    public string BandHighlightSub { get; set; } = string.Empty;

    [BsonElement("bandDescription")]
    public string BandDescription { get; set; } = string.Empty;

    [BsonElement("socialLinks")]
    public List<SocialLink> SocialLinks { get; set; } = new();

    [BsonElement("spotifyArtistId")]
    public string SpotifyArtistId { get; set; } = string.Empty;

    [BsonElement("spotifyArtistUrl")]
    public string SpotifyArtistUrl { get; set; } = string.Empty;

    [BsonElement("youtubeChannelId")]
    public string YouTubeChannelId { get; set; } = string.Empty;

    [BsonElement("youtubeChannelUrl")]
    public string YouTubeChannelUrl { get; set; } = string.Empty;

    [BsonElement("contactEmail")]
    public string ContactEmail { get; set; } = string.Empty;

    [BsonElement("seoTitle")]
    public string SeoTitle { get; set; } = string.Empty;

    [BsonElement("seoDescription")]
    public string SeoDescription { get; set; } = string.Empty;
}
