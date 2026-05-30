namespace Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Audit;

public sealed record AuditLogEntryDto(
    Guid Id,
    Guid? UserId,
    Guid? DocumentId,
    int? PageNumber,
    string Action,
    int ResultCode,
    string? IpAddress,
    DateTimeOffset AtUtc);
