namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Persistence.Entities;

public sealed class DocumentAccess
{
    public Guid Id { get; set; }
    public Guid DocumentId { get; set; }
    public Guid UserId { get; set; }
    public Guid GrantedById { get; set; }
    public DateTimeOffset GrantedAtUtc { get; set; }
}
