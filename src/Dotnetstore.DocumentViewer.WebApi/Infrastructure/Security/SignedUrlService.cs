using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Security;

internal sealed class SignedUrlService(IOptions<SignedUrlOptions> options, TimeProvider clock) : ISignedUrlService
{
    private readonly byte[] _key = Encoding.UTF8.GetBytes(options.Value.SigningKey);
    private readonly TimeSpan _lifetime = options.Value.Lifetime;

    public SignedPage Sign(Guid userId, Guid documentId, int page)
    {
        var expires = clock.GetUtcNow().Add(_lifetime).ToUnixTimeSeconds();
        var signature = Compute(userId, documentId, page, expires);
        return new SignedPage(page, expires, signature);
    }

    public bool Verify(Guid userId, Guid documentId, int page, long expiresUnix, string signature)
    {
        if (clock.GetUtcNow().ToUnixTimeSeconds() > expiresUnix)
            return false;

        var expected = Compute(userId, documentId, page, expiresUnix);
        var a = Encoding.UTF8.GetBytes(expected);
        var b = Encoding.UTF8.GetBytes(signature);
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    private string Compute(Guid userId, Guid documentId, int page, long expiresUnix)
    {
        var payload = $"{userId:N}|{documentId:N}|{page}|{expiresUnix}";
        var bytes = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload));
        return Base64UrlEncode(bytes);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
