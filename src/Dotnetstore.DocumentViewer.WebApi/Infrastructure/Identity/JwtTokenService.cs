using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence.Entities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;

internal sealed class JwtTokenService(IOptions<JwtOptions> options, AppDbContext db, TimeProvider clock)
    : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    public async Task<IssuedTokens> IssueAsync(
        ApplicationUser user,
        IEnumerable<string> roles,
        CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var accessExpires = now + _options.AccessTokenLifetime;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            // Read on every request by MustChangePasswordGuardMiddleware. Stamped here
            // (not derived from the user table per-request) so the bearer flow stays
            // round-trip-free on the hot path.
            new("mcp", user.MustChangePassword ? "1" : "0"),
        };
        claims.AddRange(roles.Select(r => new Claim("role", r)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: accessExpires.UtcDateTime,
            signingCredentials: creds);

        var accessToken = new JwtSecurityTokenHandler().WriteToken(jwt);

        var refreshToken = GenerateRefreshToken();
        var refreshExpires = now + _options.RefreshTokenLifetime;
        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashRefreshToken(refreshToken),
            CreatedAt = now,
            ExpiresAt = refreshExpires,
        });
        await db.SaveChangesAsync(ct);

        return new IssuedTokens(accessToken, accessExpires, refreshToken, refreshExpires);
    }

    public string HashRefreshToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }

    private static string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }
}
