using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Auth;

namespace Dotnetstore.DocumentViewer.Shared.SDK;

public sealed class InMemoryApiSession : IApiSession
{
    private const string AdminRole = "Admin";

    public string? AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public DateTimeOffset? AccessTokenExpiresAtUtc { get; private set; }
    public DateTimeOffset? RefreshTokenExpiresAtUtc { get; private set; }
    public MeResponse? Me { get; private set; }

    public bool IsAuthenticated => AccessToken is { Length: > 0 };
    public bool IsAdmin => Me?.Roles.Contains(AdminRole) ?? false;

    public event EventHandler? Changed;

    public void Set(string accessToken, DateTimeOffset accessExpires, string refreshToken, DateTimeOffset refreshExpires)
    {
        AccessToken = accessToken;
        AccessTokenExpiresAtUtc = accessExpires;
        RefreshToken = refreshToken;
        RefreshTokenExpiresAtUtc = refreshExpires;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetMe(MeResponse me)
    {
        Me = me;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        AccessToken = null;
        RefreshToken = null;
        AccessTokenExpiresAtUtc = null;
        RefreshTokenExpiresAtUtc = null;
        Me = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
