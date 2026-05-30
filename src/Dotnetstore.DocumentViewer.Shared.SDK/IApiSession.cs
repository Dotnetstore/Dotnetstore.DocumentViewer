namespace Dotnetstore.DocumentViewer.Shared.SDK;

public interface IApiSession
{
    string? AccessToken { get; }
    string? RefreshToken { get; }
    DateTimeOffset? AccessTokenExpiresAtUtc { get; }
    bool IsAuthenticated { get; }

    void Set(string accessToken, DateTimeOffset accessExpires, string refreshToken, DateTimeOffset refreshExpires);
    void Clear();
}
