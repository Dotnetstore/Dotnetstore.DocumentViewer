using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Auth;

namespace Dotnetstore.DocumentViewer.Shared.SDK;

public interface IApiSession
{
    string? AccessToken { get; }
    string? RefreshToken { get; }
    DateTimeOffset? AccessTokenExpiresAtUtc { get; }
    MeResponse? Me { get; }
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }

    event EventHandler? Changed;

    void Set(string accessToken, DateTimeOffset accessExpires, string refreshToken, DateTimeOffset refreshExpires);
    void SetMe(MeResponse me);
    void Clear();
}
