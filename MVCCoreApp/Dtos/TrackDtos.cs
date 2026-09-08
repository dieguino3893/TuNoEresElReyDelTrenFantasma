using MVCCoreApp.Models;

namespace MVCCoreApp.Dtos;

public record TrackDto(string Id, string Title, int TrackNumber, string Type, TrackPlatformLinks Links, string Source, string? ExternalId, bool IsVisible, int Order, DateTime CreatedAt, string CreatedBy, DateTime UpdatedAt, string UpdatedBy);
public record CreateTrackDto(string Title, int TrackNumber, string Type, TrackPlatformLinks Links, string? ExternalId, bool IsVisible, int Order);
public record UpdateTrackDto(string Title, int TrackNumber, string Type, TrackPlatformLinks Links, string? ExternalId, bool IsVisible, int Order);
