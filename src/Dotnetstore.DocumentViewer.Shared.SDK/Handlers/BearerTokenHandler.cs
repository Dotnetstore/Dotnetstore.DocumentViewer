using System.Net.Http.Headers;

namespace Dotnetstore.DocumentViewer.Shared.SDK.Handlers;

public sealed class BearerTokenHandler(IApiSession session) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.Headers.Authorization is null && session.AccessToken is { Length: > 0 } token)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return base.SendAsync(request, ct);
    }
}
