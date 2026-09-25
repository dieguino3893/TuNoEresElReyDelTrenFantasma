using MongoDB.Bson.Serialization.Attributes;
using MVCCoreApp.Models.Base;

namespace MVCCoreApp.Models;

/// <summary>Chip de la portada (los 3 sellos bajo el slogan: En vivo, Estudio, Gira...).</summary>
public class HeroChip : AuditableDocument
{
    [BsonElement("text")]
    public string Text { get; set; } = string.Empty;

    [BsonElement("icon")]
    public string Icon { get; set; } = "fa-solid fa-circle";

    [BsonElement("order")]
    public int Order { get; set; }

    [BsonElement("isVisible")]
    public bool IsVisible { get; set; } = true;
}
