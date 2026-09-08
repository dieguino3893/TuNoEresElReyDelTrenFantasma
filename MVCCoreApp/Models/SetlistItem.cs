using MongoDB.Bson.Serialization.Attributes;

namespace MVCCoreApp.Models;

public class SetlistItem
{
    [BsonElement("source")]
    public string Source { get; set; } = "Track"; // Track (del catálogo) | Custom (título libre)

    [BsonElement("trackId")]
    public string? TrackId { get; set; }

    [BsonElement("title")]
    public string Title { get; set; } = string.Empty;
}