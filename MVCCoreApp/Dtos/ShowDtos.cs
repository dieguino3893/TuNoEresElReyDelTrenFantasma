namespace MVCCoreApp.Dtos;

public record ShowDto(string Id, DateTime Date, string Time, string Venue, string CityState, string? GoogleMapsUrl, bool IsVisible, int Order, DateTime CreatedAt, string CreatedBy, DateTime UpdatedAt, string UpdatedBy);
public record CreateShowDto(DateTime Date, string Time, string Venue, string CityState, string? GoogleMapsUrl, bool IsVisible, int Order);
public record UpdateShowDto(DateTime Date, string Time, string Venue, string CityState, string? GoogleMapsUrl, bool IsVisible, int Order);
