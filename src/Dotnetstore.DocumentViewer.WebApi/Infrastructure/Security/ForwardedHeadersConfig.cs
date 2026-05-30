namespace Dotnetstore.DocumentViewer.WebApi.Infrastructure.Security;

/// <summary>
/// Friendly config shape for <see cref="Microsoft.AspNetCore.Builder.ForwardedHeadersOptions"/>.
/// IPNetwork and IPAddress can't be bound from raw strings, so this DTO carries the values
/// in string form and a PostConfigure step in Program.cs translates them onto the framework
/// options the middleware actually reads.
/// </summary>
public sealed class ForwardedHeadersConfig
{
    public const string SectionName = "ForwardedHeaders";

    /// <summary>Whether to register <c>UseForwardedHeaders()</c> in the pipeline.</summary>
    public bool Enabled { get; init; } = true;

    /// <summary>CIDR networks (e.g. <c>"10.0.0.0/8"</c>) whose connections are trusted to set X-Forwarded-*.</summary>
    public string[] KnownNetworks { get; init; } = [];

    /// <summary>Specific proxy IPs trusted to set X-Forwarded-*.</summary>
    public string[] KnownProxies { get; init; } = [];

    /// <summary>How many proxies to honour in the chain. Default 1 — increase only if you know what you're doing.</summary>
    public int ForwardLimit { get; init; } = 1;
}
