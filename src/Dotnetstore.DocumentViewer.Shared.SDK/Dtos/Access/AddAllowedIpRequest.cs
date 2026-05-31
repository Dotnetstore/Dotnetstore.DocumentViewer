namespace Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Access;

public sealed record AddAllowedIpRequest(string Cidr, string? Description);
