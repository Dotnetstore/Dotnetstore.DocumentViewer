using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Documents;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Documents.GetMetadata;

internal sealed class GetDocumentMetadataEndpoint(AppDbContext db)
    : EndpointWithoutRequest<DocumentDto>
{
    public override void Configure()
    {
        Get("/documents/{id:guid}");
        Description(b => b.WithTags("Documents"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        var documentId = Route<Guid>("id");

        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(sub, out var userId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var document = await db.Documents.AsNoTracking()
            .SingleOrDefaultAsync(d => d.Id == documentId, ct);
        if (document is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        if (!User.IsInRole(RoleNames.Admin))
        {
            var hasAccess = await db.DocumentAccesses
                .AnyAsync(a => a.DocumentId == documentId && a.UserId == userId, ct);
            if (!hasAccess)
            {
                await Send.ForbiddenAsync(ct);
                return;
            }
        }

        await Send.OkAsync(
            new DocumentDto(
                document.Id, document.Title, document.OriginalFileName, document.ContentType,
                document.PageCount, document.Status, document.UploadedById, document.UploadedAtUtc),
            ct);
    }
}
