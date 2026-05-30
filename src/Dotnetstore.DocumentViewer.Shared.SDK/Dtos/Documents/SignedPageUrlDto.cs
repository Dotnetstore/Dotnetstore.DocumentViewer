namespace Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Documents;

public sealed record SignedPageUrlDto(int Page, string Url, DateTimeOffset ExpiresAtUtc);
