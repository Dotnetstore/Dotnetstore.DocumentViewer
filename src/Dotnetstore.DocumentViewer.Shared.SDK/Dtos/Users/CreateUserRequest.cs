namespace Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Users;

public sealed record CreateUserRequest(
    string Email,
    string DisplayName,
    string Password,
    IReadOnlyList<string> Roles);
