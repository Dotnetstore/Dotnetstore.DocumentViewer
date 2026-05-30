using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Documents;

namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence.Entities;

public sealed class Document
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public int PageCount { get; set; }
    public string StoragePath { get; set; } = string.Empty;
    public Guid UploadedById { get; set; }
    public DateTimeOffset UploadedAtUtc { get; set; }
    public DocumentStatus Status { get; set; }
}
