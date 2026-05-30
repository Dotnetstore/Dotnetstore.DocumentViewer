namespace Dotnetstore.DocumentViewer.Shared.SDK;

public sealed class InMemoryApiSession : IApiSession
{
    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTimeOffset? AccessTokenExpiresAtUtc { get; private set; }
    public DateTimeOffset? RefreshTokenExpiresAtUtc { get; private set; }

    public bool IsAuthenticated => AccessToken is { Length: > 0 };

    public void Set(string accessToken, DateTimeOffset accessExpires, string refreshToken, DateTimeOffset refreshExpires)
    {
        AccessToken = accessToken;
        AccessTokenExpiresAtUtc = accessExpires;
        RefreshToken = refreshToken;
        RefreshTokenExpiresAtUtc = refreshExpires;
    }

    public void Clear()
    {
        AccessToken = null;
        RefreshToken = null;
        AccessTokenExpiresAtUtc = null;
        RefreshTokenExpiresAtUtc = null;
    }
}
