namespace Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Access;

public sealed record DocumentAccessDto(
    Guid Id,
    Guid DocumentId,
    Guid UserId,
    string? UserEmail,
    Guid GrantedById,
    DateTimeOffset GrantedAtUtc);
