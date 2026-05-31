using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Security;

/// <summary>
/// jti blacklist backed by the <see cref="AppDbContext"/> with an <see cref="IMemoryCache"/>
/// front so the hot path (every authenticated request) doesn't round-trip to Postgres.
/// Cache TTL exceeds the access-token lifetime, so a single DB read covers the token's life.
/// </summary>
internal sealed class AccessTokenRevocationStore(AppDbContext db, IMemoryCache cache, TimeProvider clock)
    : IAccessTokenRevocationStore
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(20);
    private const string CachePrefix = "jti:";

    public async Task RevokeAsync(string jti, Guid userId, DateTimeOffset expiresAt, CancellationToken ct)
    {
        var exists = await db.RevokedAccessTokens.AnyAsync(t => t.Jti == jti, ct);
        if (!exists)
        {
            db.RevokedAccessTokens.Add(new RevokedAccessToken
            {
                Jti = jti,
                UserId = userId,
                ExpiresAt = expiresAt,
                RevokedAt = clock.GetUtcNow(),
            });
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { /* race: another caller just inserted the same jti */ }
        }
        cache.Set(CachePrefix + jti, true, CacheTtl);
    }

    public async Task<bool> IsRevokedAsync(string jti, CancellationToken ct)
    {
        if (cache.TryGetValue(CachePrefix + jti, out bool cached) && cached)
            return true;

        var revoked = await db.RevokedAccessTokens.AnyAsync(t => t.Jti == jti, ct);
        if (revoked) cache.Set(CachePrefix + jti, true, CacheTtl);
        return revoked;
    }
}
