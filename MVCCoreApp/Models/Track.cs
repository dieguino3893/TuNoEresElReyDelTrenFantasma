using MongoDB.Bson.Serialization.Attributes;
using MVCCoreApp.Models.Base;

namespace MVCCoreApp.Models;

public class Track : AuditableDocument
{
    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;

    [BsonElement("trackNumber")]
    public int TrackNumber { get; set; }

    [BsonElement("type")]
    public string Type { get; set; } = "Single";

    [BsonElement("links")]
    public TrackPlatformLinks Links { get; set; } = new();

    /// <summary>Audio propio en Cloudinary (mp3/m4a). Se usa en el reproductor de ensayo sin anuncios.</summary>
    [BsonElement("audioUrl")]
    public string? AudioUrl { get; set; }

    [BsonElement("source")]
    public string Source { get; set; } = "Manual"; // Manual | SpotifyApi | YouTubeApi

    [BsonElement("externalId")]
    public string? ExternalId { get; set; } // SpotifyId

    [BsonElement("isVisible")]
    public bool IsVisible { get; set; } = true;

    [BsonElement("order")]
    public int Order { get; set; }
}
