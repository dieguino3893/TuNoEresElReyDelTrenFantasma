using MongoDB.Bson.Serialization.Attributes;
using MVCCoreApp.Models.Base;

namespace MVCCoreApp.Models;

public class Media : AuditableDocument
{
    [BsonElement("name")]
    public string Name { get; set; } = string.Empty; // nombre puesto al subir

    [BsonElement("url")]
    public string Url { get; set; } = string.Empty; // secure_url Cloudinary

    [BsonElement("publicId")]
    public string PublicId { get; set; } = string.Empty; // elrey/...

    [BsonElement("folder")]
    public string Folder { get; set; } = "elrey";

    [BsonElement("format")]
    public string? Format { get; set; }

    [BsonElement("bytes")]
    public long Bytes { get; set; }
}
