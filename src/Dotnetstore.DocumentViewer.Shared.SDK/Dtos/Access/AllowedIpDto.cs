namespace Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Access;

public sealed record AllowedIpDto(
    Guid Id,
    Guid DocumentId,
    string Cidr,
    string? Description,
    Guid AddedById,
    DateTimeOffset AddedAtUtc);
