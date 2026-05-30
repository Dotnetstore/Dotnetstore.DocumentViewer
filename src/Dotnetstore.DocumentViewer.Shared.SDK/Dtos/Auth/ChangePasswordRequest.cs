namespace Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Auth;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
