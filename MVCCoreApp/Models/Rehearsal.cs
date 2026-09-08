using MongoDB.Bson.Serialization.Attributes;
using MVCCoreApp.Models.Base;

namespace MVCCoreApp.Models;

public class Rehearsal : AuditableDocument
{
    [BsonElement("date")]
    public DateTime Date { get; set; } = DateTime.UtcNow;

    [BsonElement("place")]
    public string Place { get; set; } = string.Empty;

    [BsonElement("notes")]
    public string? Notes { get; set; }

    [BsonElement("setlistId")]
    public string? SetlistId { get; set; }

    [BsonElement("showId")]
    public string? ShowId { get; set; }

    [BsonElement("startTime")]
    public string? StartTime { get; set; }

    [BsonElement("endTime")]
    public string? EndTime { get; set; }
}