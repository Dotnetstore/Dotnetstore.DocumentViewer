using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Documents;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Documents.List;

internal sealed class ListDocumentsEndpoint(AppDbContext db)
    : EndpointWithoutRequest<IReadOnlyList<DocumentDto>>
{
    public override void Configure()
    {
        Get("/documents");
        Description(b => b.WithTags("Documents"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(sub, out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var query = db.Documents.AsNoTracking();

        if (!User.IsInRole(RoleNames.Admin))
        {
            query = query.Where(d =>
                db.DocumentAccesses.Any(a => a.DocumentId == d.Id && a.UserId == userId));
        }

        var rows = await query
            .OrderByDescending(d => d.UploadedAtUtc)
            .Select(d => new DocumentDto(
                d.Id, d.Title, d.OriginalFileName, d.ContentType,
                d.PageCount, d.Status, d.UploadedById, d.UploadedAtUtc))
            .ToListAsync(ct);

        await Send.OkAsync(rows, ct);
    }
}
