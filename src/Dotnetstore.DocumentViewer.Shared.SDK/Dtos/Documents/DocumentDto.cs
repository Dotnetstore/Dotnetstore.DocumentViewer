namespace Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Documents;

public sealed record DocumentDto(
    Guid Id,
    string Title,
    string OriginalFileName,
    string ContentType,
    int PageCount,
    DocumentStatus Status,
    Guid UploadedById,
    DateTimeOffset UploadedAtUtc);
