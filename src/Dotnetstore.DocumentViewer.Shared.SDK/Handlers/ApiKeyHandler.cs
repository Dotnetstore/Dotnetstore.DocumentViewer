using Microsoft.Extensions.Options;

namespace Dotnetstore.DocumentViewer.Shared.SDK.Handlers;

public sealed class ApiKeyHandler(IOptions<ApiClientOptions> options) : DelegatingHandler
{
    private const string HeaderName = "X-Api-Key";
    private readonly string _apiKey = options.Value.ApiKey;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (!request.Headers.Contains(HeaderName))
            request.Headers.Add(HeaderName, _apiKey);
        return base.SendAsync(request, ct);
    }
}
