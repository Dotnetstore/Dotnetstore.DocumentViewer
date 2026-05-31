using System.Net;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Security;

internal sealed class DocumentIpPolicy(AppDbContext db) : IDocumentIpPolicy
{
    public async Task<bool> IsAllowedAsync(Guid documentId, bool isAdmin, IPAddress? clientIp, CancellationToken ct)
    {
        if (isAdmin) return true;
        if (clientIp is null) return false;

        var cidrs = await db.DocumentAllowedIps
            .Where(a => a.DocumentId == documentId)
            .Select(a => a.Cidr)
            .ToListAsync(ct);

        if (cidrs.Count == 0) return false;

        foreach (var cidr in cidrs)
        {
            if (IPNetwork.TryParse(cidr, out var network) && network.Contains(clientIp))
                return true;
        }
        return false;
    }
}
