using MongoDB.Bson.Serialization.Attributes;

namespace MVCCoreApp.Models;

public class TrackPlatformLinks
{
    [BsonElement("spotifyUrl")]
    public string? SpotifyUrl { get; set; }

    [BsonElement("spotifyTrackId")]
    public string? SpotifyTrackId { get; set; }

    [BsonElement("spotifyEmbedUrl")]
    public string? SpotifyEmbedUrl { get; set; }

    [BsonElement("appleMusicUrl")]
    public string? AppleMusicUrl { get; set; }

    [BsonElement("youtubeUrl")]
    public string? YouTubeUrl { get; set; }
}
