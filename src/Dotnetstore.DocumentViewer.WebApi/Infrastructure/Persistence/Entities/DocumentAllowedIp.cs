namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence.Entities;

public sealed class DocumentAllowedIp
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public string Cidr { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid AddedById { get; set; }
    public DateTimeOffset AddedAtUtc { get; set; }
}
