using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Access;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence.Entities;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Access.Grant;

internal sealed class GrantAccessEndpoint(AppDbContext db, TimeProvider clock)
    : Endpoint<GrantAccessRequest, DocumentAccessDto>
{
    public override void Configure()
    {
        Post("/documents/{id:guid}/access");
        Roles(RoleNames.Admin);
        Description(b => b.WithTags("Access"));
    }

    public override async Task HandleAsync(GrantAccessRequest req, CancellationToken ct)
    {
        var documentId = Route<Guid>("id");

        var document = await db.Documents.SingleOrDefaultAsync(d => d.Id == documentId, ct);
        if (document is null)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        var user = await db.Users
            .Where(u => u.Id == req.UserId)
            .Select(u => new { u.Id, u.Email })
            .SingleOrDefaultAsync(ct);
        if (user is null)
        {
            AddError(r => r.UserId, "User does not exist.");
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
            return;
        }

        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(sub, out var grantedById))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var existing = await db.DocumentAccesses
            .SingleOrDefaultAsync(a => a.DocumentId == documentId && a.UserId == req.UserId, ct);
        if (existing is not null)
        {
            await Send.OkAsync(
                new DocumentAccessDto(existing.Id, existing.DocumentId, existing.UserId, user.Email,
                    existing.GrantedById, existing.GrantedAtUtc),
                ct);
            return;
        }

        var access = new DocumentAccess
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            UserId = req.UserId,
            GrantedById = grantedById,
            GrantedAtUtc = clock.GetUtcNow(),
        };
        db.DocumentAccesses.Add(access);
        await db.SaveChangesAsync(ct);

        await Send.OkAsync(
            new DocumentAccessDto(access.Id, access.DocumentId, access.UserId, user.Email,
                access.GrantedById, access.GrantedAtUtc),
            ct);
    }
}
