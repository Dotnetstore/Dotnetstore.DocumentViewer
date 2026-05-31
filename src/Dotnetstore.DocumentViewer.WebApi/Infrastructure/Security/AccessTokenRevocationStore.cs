using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Security;

/// <summary>
/// jti blacklist backed by the <see cref="AppDbContext"/> with an <see cref="IMemoryCache"/>
/// front so the hot path (every authenticated request) doesn't round-trip to Postgres.
/// Cache TTL tracks <see cref="JwtOptions.AccessTokenLifetime"/> so the cache window always
/// covers the access-token lifetime — a token can't outlive its own cached revocation.
/// </summary>
internal sealed class AccessTokenRevocationStore : IAccessTokenRevocationStore
{
    private const string CachePrefix = "jti:";

    private readonly AppDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _clock;
    private readonly TimeSpan _cacheTtl;

    public AccessTokenRevocationStore(
        AppDbContext db,
        IMemoryCache cache,
        IOptions<JwtOptions> jwtOptions,
        TimeProvider clock)
    {
        _db = db;
        _cache = cache;
        _clock = clock;
        // Pad slightly so a token at exactly the lifetime edge still finds its cached entry.
        _cacheTtl = jwtOptions.Value.AccessTokenLifetime + TimeSpan.FromMinutes(1);
    }

    public async Task RevokeAsync(string jti, Guid userId, DateTimeOffset expiresAt, CancellationToken ct)
    {
        var exists = await _db.RevokedAccessTokens.AnyAsync(t => t.Jti == jti, ct);
        if (!exists)
        {
            _db.RevokedAccessTokens.Add(new RevokedAccessToken
            {
                Jti = jti,
                UserId = userId,
                ExpiresAt = expiresAt,
                RevokedAt = _clock.GetUtcNow(),
            });
            try { await _db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { /* race: another caller just inserted the same jti */ }
        }
        _cache.Set(CachePrefix + jti, true, _cacheTtl);
    }

    public async Task<bool> IsRevokedAsync(string jti, CancellationToken ct)
    {
        if (_cache.TryGetValue(CachePrefix + jti, out bool cached) && cached)
            return true;

        var revoked = await _db.RevokedAccessTokens.AnyAsync(t => t.Jti == jti, ct);
        if (revoked) _cache.Set(CachePrefix + jti, true, _cacheTtl);
        return revoked;
    }
}
