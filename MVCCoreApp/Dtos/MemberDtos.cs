namespace MVCCoreApp.Dtos;

public record MemberDto(string Id, string Name, string Role, string ImageUrl, string ImageAlt, string ProfileUrl, int Order, bool IsActive, DateTime CreatedAt, string CreatedBy, DateTime UpdatedAt, string UpdatedBy);
public record CreateMemberDto(string Name, string Role, string ImageUrl, string ImageAlt, string ProfileUrl, int Order, bool IsActive);
public record UpdateMemberDto(string Name, string Role, string ImageUrl, string ImageAlt, string ProfileUrl, int Order, bool IsActive);
