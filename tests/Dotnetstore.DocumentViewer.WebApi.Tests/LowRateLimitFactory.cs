using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Dotnetstore.DocumentViewer.WebApi.Tests;

/// <summary>
/// Same as <see cref="DocumentViewerApiFactory"/> but overrides the auth rate limit
/// down to a few requests per minute so a dedicated test can prove the 429 path fires.
/// Each test instance gets its own Postgres container — slightly slower setup, but
/// keeps the shared collection fixture's permissive limits intact.
/// </summary>
public sealed class LowRateLimitFactory : DocumentViewerApiFactory
{
    public const int PermitLimit = 3;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // ConfigureAppConfiguration appends another provider on top of the base's;
        // last-added wins, so these override the base's permissive 10000 limit.
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:Auth:PermitLimit"] = PermitLimit.ToString(),
                ["RateLimiting:Auth:Window"] = "00:01:00",
            });
        });
    }
}
