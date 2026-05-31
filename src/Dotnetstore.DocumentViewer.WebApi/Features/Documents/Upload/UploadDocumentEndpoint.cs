using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Documents;
using Dotnetstore.DocumentViewer.WebApi.Infrastructure.Auditing;
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
    IAuditLogger audit,
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

        // Classify by sniffing the first bytes of the payload. Trusting the content-type
        // header or filename extension alone lets a renamed payload slip through; %PDF /
        // PK zip-magic are cheap, can't be spoofed by metadata, and let us accept the
        // file even when the client sends application/octet-stream.
        await using var stream = file.OpenReadStream();
        var kind = await ClassifyAsync(stream, ct);
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
        audit.Add(AuditActions.DocumentUploaded,
            userId: uploaderId,
            documentId: documentId,
            resultCode: StatusCodes.Status200OK,
            ipAddress: HttpContext.Connection.RemoteIpAddress?.ToString());
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

    private static readonly byte[] PdfMagic = "%PDF-"u8.ToArray();
    private static readonly byte[] ZipMagic = [0x50, 0x4B, 0x03, 0x04]; // "PK\x03\x04" — DOCX is a ZIP

    private static async Task<UploadKind?> ClassifyAsync(Stream stream, CancellationToken ct)
    {
        var buffer = new byte[8];
        var read = await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, ct);
        if (stream.CanSeek) stream.Position = 0;
        if (read < 4) return null;

        if (buffer.AsSpan(0, PdfMagic.Length).SequenceEqual(PdfMagic))
            return new UploadKind(PdfContentType, ".pdf", IsPdf: true);

        if (buffer.AsSpan(0, ZipMagic.Length).SequenceEqual(ZipMagic))
            return new UploadKind(DocxContentType, ".docx", IsPdf: false);

        return null;
    }

    private readonly record struct UploadKind(string ContentType, string PreferredExtension, bool IsPdf);
}
