namespace Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Users;

public sealed record UpdateUserRequest(
    string DisplayName,
    IReadOnlyList<string> Roles);
