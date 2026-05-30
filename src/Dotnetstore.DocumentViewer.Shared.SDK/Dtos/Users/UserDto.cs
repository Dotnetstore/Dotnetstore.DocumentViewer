namespace Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Users;

public sealed record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    bool MustChangePassword);
