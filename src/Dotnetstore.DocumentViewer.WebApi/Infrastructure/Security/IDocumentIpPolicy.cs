using System.Net;

namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Security;

/// <summary>
/// Decides whether a request from <paramref name="clientIp"/> may render a given document.
/// Admins always pass; viewers must match a CIDR on the document's allow-list. An empty
/// allow-list denies all non-admin viewers (closed by default).
/// </summary>
public interface IDocumentIpPolicy
{
    Task<bool> IsAllowedAsync(Guid documentId, bool isAdmin, IPAddress? clientIp, CancellationToken ct);
}
