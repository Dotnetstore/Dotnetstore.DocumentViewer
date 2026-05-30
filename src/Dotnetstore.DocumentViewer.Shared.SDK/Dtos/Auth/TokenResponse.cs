namespace Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Auth;

public sealed record TokenResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
