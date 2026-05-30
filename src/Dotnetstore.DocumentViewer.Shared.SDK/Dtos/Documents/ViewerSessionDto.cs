namespace Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Documents;

public sealed record ViewerSessionDto(
    DocumentDto Document,
    IReadOnlyList<SignedPageUrlDto> Pages);
