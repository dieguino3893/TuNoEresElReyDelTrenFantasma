using MongoDB.Bson.Serialization.Attributes;
using MVCCoreApp.Models.Base;

namespace MVCCoreApp.Models;

public class ApiKey : AuditableDocument
{
    [BsonElement("keyHash")]
    public string KeyHash { get; set; } = string.Empty; // SHA256

    [BsonElement("prefix")]
    public string Prefix { get; set; } = string.Empty; // primeros 8 chars para mostrar

    [BsonElement("username")]
    public string Username { get; set; } = string.Empty;

    [BsonElement("userId")]
    public string UserId { get; set; } = string.Empty;

    [BsonElement("role")]
    public string Role { get; set; } = "Admin";

    [BsonElement("sessionId")]
    public string SessionId { get; set; } = string.Empty; // HttpContext.Session Id o trace

    [BsonElement("expiresAt")]
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(8);

    [BsonElement("lastUsedAt")]
    public DateTime? LastUsedAt { get; set; }

    [BsonElement("isActive")]
    public bool IsActive { get; set; } = true;

    [BsonElement("isRevoked")]
    public bool IsRevoked { get; set; } = false;
}
