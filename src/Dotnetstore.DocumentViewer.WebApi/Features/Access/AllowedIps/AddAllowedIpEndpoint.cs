using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Access;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence.Entities;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Access.AllowedIps;

internal sealed class AddAllowedIpEndpoint(AppDbContext db, TimeProvider clock)
    : Endpoint<AddAllowedIpRequest, AllowedIpDto>
{
    public override void Configure()
    {
        Post("/documents/{id:guid}/allowed-ips");
        Roles(RoleNames.Admin);
        Description(b => b.WithTags("Access"));
    }

    public override async Task HandleAsync(AddAllowedIpRequest req, CancellationToken ct)
    {
        var documentId = Route<Guid>("id");

        var documentExists = await db.Documents.AnyAsync(d => d.Id == documentId, ct);
        if (!documentExists)
        {
            await Send.NotFoundAsync(ct);
            return;
        }

        // Validator already proved the input parses; normalise so duplicates collapse
        // ("10.0.0.5" and "10.0.0.5/32" become the same canonical key).
        if (!NormalizeCidr.TryNormalize(req.Cidr, out var cidr))
        {
            AddError(r => r.Cidr, "Invalid CIDR.");
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
            return;
        }

        if (!User.TryGetUserId(out var actorId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var existing = await db.DocumentAllowedIps
            .SingleOrDefaultAsync(a => a.DocumentId == documentId && a.Cidr == cidr, ct);
        if (existing is not null)
        {
            await Send.OkAsync(
                new AllowedIpDto(existing.Id, existing.DocumentId, existing.Cidr,
                    existing.Description, existing.AddedById, existing.AddedAtUtc),
                ct);
            return;
        }

        var entry = new DocumentAllowedIp
        {
            Id = Guid.NewGuid(),
            DocumentId = documentId,
            Cidr = cidr,
            Description = req.Description,
            AddedById = actorId,
            AddedAtUtc = clock.GetUtcNow(),
        };
        db.DocumentAllowedIps.Add(entry);
        await db.SaveChangesAsync(ct);

        await Send.OkAsync(
            new AllowedIpDto(entry.Id, entry.DocumentId, entry.Cidr,
                entry.Description, entry.AddedById, entry.AddedAtUtc),
            ct);
    }
}
