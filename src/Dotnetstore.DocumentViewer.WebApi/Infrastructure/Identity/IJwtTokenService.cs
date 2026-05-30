using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence.Entities;

namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;

public sealed record IssuedTokens(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);

public interface IJwtTokenService
{
    Task<IssuedTokens> IssueAsync(ApplicationUser user, IEnumerable<string> roles, CancellationToken ct);
    string HashRefreshToken(string token);
}
