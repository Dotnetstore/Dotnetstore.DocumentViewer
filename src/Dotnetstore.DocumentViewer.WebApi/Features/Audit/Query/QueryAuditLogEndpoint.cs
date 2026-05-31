using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Audit;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Audit.Query;

internal sealed class QueryAuditLogEndpoint(AppDbContext db)
    : EndpointWithoutRequest<IReadOnlyList<AuditLogEntryDto>>
{
    private const int DefaultTake = 100;
    private const int MaxTake = 1000;

    public override void Configure()
    {
        Get("/audit-log");
        Roles(RoleNames.Admin);
        Description(b => b.WithTags("Audit"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var userId = Query<Guid?>("userId", isRequired: false);
        var documentId = Query<Guid?>("documentId", isRequired: false);
        var actionPrefix = Query<string?>("action", isRequired: false);
        var fromUtc = Query<DateTimeOffset?>("fromUtc", isRequired: false);
        var toUtc = Query<DateTimeOffset?>("toUtc", isRequired: false);
        var take = Query<int?>("take", isRequired: false) ?? DefaultTake;
        if (take <= 0) take = DefaultTake;
        if (take > MaxTake) take = MaxTake;

        var query = db.AccessAuditLogs.AsNoTracking();
        if (userId.HasValue) query = query.Where(a => a.UserId == userId.Value);
        if (documentId.HasValue) query = query.Where(a => a.DocumentId == documentId.Value);
        if (!string.IsNullOrWhiteSpace(actionPrefix))
            query = query.Where(a => a.Action.StartsWith(actionPrefix));
        if (fromUtc.HasValue) query = query.Where(a => a.AtUtc >= fromUtc.Value);
        if (toUtc.HasValue) query = query.Where(a => a.AtUtc < toUtc.Value);

        // LEFT JOIN AspNetUsers so each row carries the email of the acting user — the
        // raw UserId Guid is unreadable in the audit UI. Mirrors the projection pattern
        // already used by ListAccessForDocumentEndpoint.
        var rows = await (
            from a in query.OrderByDescending(a => a.AtUtc).Take(take)
            join u in db.Users on a.UserId equals u.Id into uu
            from u in uu.DefaultIfEmpty()
            select new AuditLogEntryDto(
                a.Id, a.UserId, a.DocumentId, a.PageNumber,
                a.Action, a.ResultCode, a.IpAddress, a.AtUtc,
                u != null ? u.Email : null))
            .ToListAsync(ct);

        await Send.OkAsync(rows, ct);
    }
}
