using MongoDB.Bson.Serialization.Attributes;
using MVCCoreApp.Models.Base;

namespace MVCCoreApp.Models;

public class Show : AuditableDocument
{
    [BsonElement("date")]
    public DateTime Date { get; set; }

    [BsonElement("time")]
    public string Time { get; set; } = "21:00";

    [BsonElement("venue")]
    public string Venue { get; set; } = string.Empty;

    [BsonElement("cityState")]
    public string CityState { get; set; } = string.Empty;

    [BsonElement("googleMapsUrl")]
    public string? GoogleMapsUrl { get; set; } // URL copiada de Google

    [BsonElement("isVisible")]
    public bool IsVisible { get; set; } = true;

    [BsonElement("order")]
    public int Order { get; set; }
}
