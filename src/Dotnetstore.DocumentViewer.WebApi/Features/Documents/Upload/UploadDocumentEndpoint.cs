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
    private const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

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
        var kind = Classify(file.ContentType, extension);
        if (kind is null)
        {
            AddError("file", "Only PDF and DOCX documents are supported.");
            await Send.ErrorsAsync(StatusCodes.Status415UnsupportedMediaType, ct);
            return;
        }

        if (!User.TryGetUserId(out var uploaderId))
        {
            await Send.UnauthorizedAsync(ct);
            return;
        }

        var documentId = Guid.NewGuid();
        var title = (Form["Title"].ToString() is { Length: > 0 } t)
            ? t
            : Path.GetFileNameWithoutExtension(file.FileName);

        await using var stream = file.OpenReadStream();
        var storagePath = await storage.StoreAsync(stream, documentId, kind.Value.PreferredExtension, ct);

        var doc = new Document
        {
            Id = documentId,
            Title = title,
            OriginalFileName = Path.GetFileName(file.FileName),
            ContentType = kind.Value.ContentType,
            PageCount = 0,
            StoragePath = storagePath,
            UploadedById = uploaderId,
            UploadedAtUtc = clock.GetUtcNow(),
            Status = kind.Value.IsPdf ? DocumentStatus.Ready : DocumentStatus.Converting,
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

    private static UploadKind? Classify(string? contentType, string extension)
    {
        if (string.Equals(contentType, PdfContentType, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
            return new UploadKind(PdfContentType, ".pdf", IsPdf: true);

        if (string.Equals(contentType, DocxContentType, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(extension, ".docx", StringComparison.OrdinalIgnoreCase))
            return new UploadKind(DocxContentType, ".docx", IsPdf: false);

        return null;
    }

    private readonly record struct UploadKind(string ContentType, string PreferredExtension, bool IsPdf);
}
