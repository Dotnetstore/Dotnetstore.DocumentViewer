namespace Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Auth;

public sealed record MeResponse(
    Guid Id,
    string Email,
    string DisplayName,
    IReadOnlyList<string> Roles,
    bool MustChangePassword);
