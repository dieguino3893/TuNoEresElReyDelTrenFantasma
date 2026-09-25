using MongoDB.Bson.Serialization.Attributes;

namespace MVCCoreApp.Models;

/// <summary>Texto editable del sitio (clave-valor). Las claves y valores por defecto viven en SiteTextDefaults.</summary>
public class SiteText
{
    [BsonId]
    public string Key { get; set; } = string.Empty;

    [BsonElement("group")]
    public string Group { get; set; } = string.Empty;

    [BsonElement("label")]
    public string Label { get; set; } = string.Empty;

    [BsonElement("value")]
    public string Value { get; set; } = string.Empty;

    [BsonElement("multiline")]
    public bool Multiline { get; set; }

    [BsonElement("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("updatedBy")]
    public string UpdatedBy { get; set; } = "system";
}
