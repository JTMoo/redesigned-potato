namespace UserService.Application.DTOs;

public sealed record UserDto(Guid Id, string Email, string DisplayName);

public sealed record PreferenceDto(Guid Id, string PreferenceKey, string Value);
