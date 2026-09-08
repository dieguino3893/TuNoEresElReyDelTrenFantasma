using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace MVCCoreApp.Models.Base;

public abstract class AuditableDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = ObjectId.GenerateNewId().ToString();

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
}
