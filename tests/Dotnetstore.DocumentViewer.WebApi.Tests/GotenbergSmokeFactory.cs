using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Dotnetstore.DocumentViewer.WebApi.Tests;

/// <summary>
/// End-to-end fixture for the DOCX → PDF conversion path. Spins a real
/// gotenberg/gotenberg:8 container alongside Postgres, points the WebApi at it,
/// and keeps DocumentConversionWorker enabled so an uploaded DOCX actually flows
/// through Gotenberg and emerges as a watermarked PDF.
/// </summary>
public sealed class GotenbergSmokeFactory : DocumentViewerApiFactory
{
    private const int GotenbergPort = 3000;

    private readonly IContainer _gotenberg = new ContainerBuilder("gotenberg/gotenberg:8")
        .WithPortBinding(GotenbergPort, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer()
            .UntilHttpRequestIsSucceeded(r => r.ForPath("/health").ForPort(GotenbergPort)))
        .Build();

    protected override bool DisableConversionWorker => false;

    public override async ValueTask InitializeAsync()
    {
        await _gotenberg.StartAsync();
        await base.InitializeAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _gotenberg.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);

        // Last-added in-memory provider wins; point WebApi at the running Gotenberg
        // container and tighten the worker poll so the test doesn't sit for 5+ seconds.
        var gotenbergUrl = $"http://{_gotenberg.Hostname}:{_gotenberg.GetMappedPublicPort(GotenbergPort)}";
        builder.ConfigureAppConfiguration((_, cfg) =>
        {
            cfg.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DocumentConversion:Mode"] = "Gotenberg",
                ["DocumentConversion:Gotenberg:BaseAddress"] = gotenbergUrl,
                ["DocumentConversion:PollInterval"] = "00:00:01",
                ["DocumentConversion:ConversionTimeout"] = "00:01:00",
            });
        });
    }
}
