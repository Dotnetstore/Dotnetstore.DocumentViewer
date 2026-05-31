namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence.Entities;

/// <summary>
/// Per-jti blacklist row. Inserted when an access token is explicitly revoked
/// (logout, future "kick this user out" flows). The JwtBearer OnTokenValidated
/// hook checks for the jti's presence here on every authenticated request.
/// <see cref="ExpiresAt"/> mirrors the JWT's natural expiry so a cleanup worker
/// can prune rows once the token would have died on its own.
/// </summary>
public sealed class RevokedAccessToken
{
    public required string Jti { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset RevokedAt { get; set; }
}
