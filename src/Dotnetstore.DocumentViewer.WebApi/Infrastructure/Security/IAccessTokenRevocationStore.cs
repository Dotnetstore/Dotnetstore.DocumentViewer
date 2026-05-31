namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Security;

public interface IAccessTokenRevocationStore
{
    Task RevokeAsync(string jti, Guid userId, DateTimeOffset expiresAt, CancellationToken ct);
    Task<bool> IsRevokedAsync(string jti, CancellationToken ct);
}
