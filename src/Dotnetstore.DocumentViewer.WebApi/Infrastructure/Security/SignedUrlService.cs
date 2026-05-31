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
        var signature = Base64UrlEncode(ComputeHmac(userId, documentId, page, expires));
        return new SignedPage(page, expires, signature);
    }

    public bool Verify(Guid userId, Guid documentId, int page, long expiresUnix, string signature)
    {
        if (clock.GetUtcNow().ToUnixTimeSeconds() > expiresUnix)
            return false;

        // Compare on the raw 32-byte HMAC instead of base64 strings — shorter, no
        // text-encoding round-trip, and FixedTimeEquals returns false on length mismatch
        // (which covers any malformed signature without an exception).
        if (!TryBase64UrlDecode(signature, out var supplied))
            return false;

        var expected = ComputeHmac(userId, documentId, page, expiresUnix);
        return CryptographicOperations.FixedTimeEquals(expected, supplied);
    }

    private byte[] ComputeHmac(Guid userId, Guid documentId, int page, long expiresUnix)
    {
        var payload = $"{userId:N}|{documentId:N}|{page}|{expiresUnix}";
        return HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload));
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool TryBase64UrlDecode(string input, out byte[] bytes)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        switch (padded.Length % 4)
        {
            case 2: padded += "=="; break;
            case 3: padded += "="; break;
            case 1: bytes = []; return false;
        }
        try
        {
            bytes = Convert.FromBase64String(padded);
            return true;
        }
        catch (FormatException)
        {
            bytes = [];
            return false;
        }
    }
}
