namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Security;

public sealed record SignedPage(int Page, long ExpiresUnix, string Signature);

public interface ISignedUrlService
{
    SignedPage Sign(Guid userId, Guid documentId, int page);
    bool Verify(Guid userId, Guid documentId, int page, long expiresUnix, string signature);
}
