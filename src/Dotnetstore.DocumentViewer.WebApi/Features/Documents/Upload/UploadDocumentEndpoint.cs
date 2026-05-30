using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Documents;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Identity;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence.Entities;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Storage;
using FastEndpoints;
using Microsoft.Extensions.Options;

namespace Dotnetstore.DocumentViewer.WebApi.Features.Documents.Upload;

internal sealed class UploadDocumentEndpoint(
    AppDbContext db,
    IDocumentStorage storage,
    IOptions<DocumentStorageOptions> storageOptions,
    TimeProvider clock) : EndpointWithoutRequest<DocumentDto>
{
    private const string PdfContentType = "application/pdf";

    public override void Configure()
    {
        Post("/documents");
        Roles(RoleNames.Admin);
        AllowFileUploads();
        Description(b => b.WithTags("Documents"));
    }

    public override async Task HandleAsync(CancellationToken ct)
    {
        if (Files.Count == 0)
        {
            AddError("file", "A file must be provided.");
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
            return;
        }
        var file = Files[0];
        var maxBytes = storageOptions.Value.MaxBytes;
        if (file.Length > maxBytes)
        {
            AddError("file", $"File exceeds the maximum allowed size of {maxBytes} bytes.");
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, ct);
            return;
        }

        var extension = Path.GetExtension(file.FileName);
        if (!IsSupported(file.ContentType, extension))
        {
            AddError("file", "Only PDF documents are supported in this release.");
            await Send.ErrorsAsync(StatusCodes.Status415UnsupportedMediaType, ct);
            return;
        }

        var sub = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(sub, out var uploaderId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var documentId = Guid.NewGuid();
        var title = (Form["Title"].ToString() is { Length: > 0 } t)
            ? t
            : Path.GetFileNameWithoutExtension(file.FileName);

        await using var stream = file.OpenReadStream();
        var storagePath = await storage.StoreAsync(stream, documentId, extension, ct);

        var doc = new Document
        {
            Id = documentId,
            Title = title,
            OriginalFileName = Path.GetFileName(file.FileName),
            ContentType = PdfContentType,
            PageCount = 0,
            StoragePath = storagePath,
            UploadedById = uploaderId,
            UploadedAtUtc = clock.GetUtcNow(),
            Status = DocumentStatus.Ready,
        };
        db.Documents.Add(doc);
        await db.SaveChangesAsync(ct);

        await Send.OkAsync(
            new DocumentDto(
                doc.Id,
                doc.Title,
                doc.OriginalFileName,
                doc.ContentType,
                doc.PageCount,
                doc.Status,
                doc.UploadedById,
                doc.UploadedAtUtc),
            ct);
    }

    private static bool IsSupported(string? contentType, string extension) =>
        string.Equals(contentType, PdfContentType, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase);
}
