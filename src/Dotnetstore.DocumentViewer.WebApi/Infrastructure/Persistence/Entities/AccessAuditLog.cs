namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence.Entities;

public sealed class AccessAuditLog
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public Guid? DocumentId { get; set; }
    public int? PageNumber { get; set; }
    public string Action { get; set; } = string.Empty;
    public int ResultCode { get; set; }
    public string? IpAddress { get; set; }
    public DateTimeOffset AtUtc { get; set; }
}
