using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Audit;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Documents;
using Shouldly;

namespace Dotnetstore.DocumentViewer.WebApi.Tests;

[Collection(nameof(DocumentViewerApiCollection))]
public sealed class ForwardedHeadersTests(DocumentViewerApiFactory factory)
{
    [Fact]
    public async Task X_Forwarded_For_is_recorded_in_the_audit_log()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var doc = await UploadAsync(admin, "Forwarded-headers audit");

        // Loopback is in the default KnownNetworks of ForwardedHeadersOptions, so the
        // middleware trusts the X-Forwarded-For coming from the TestServer's peer.
        var forwardedIp = $"203.0.113.{Random.Shared.Next(1, 254)}";
        using var req = new HttpRequestMessage(HttpMethod.Get, $"/documents/{doc.Id}/pages/0");
        req.Headers.Add("X-Forwarded-For", forwardedIp);
        var renderAttempt = await admin.SendAsync(req);
        renderAttempt.StatusCode.ShouldBe(HttpStatusCode.Unauthorized); // missing signature

        var rows = await admin.GetFromJsonAsync<List<AuditLogEntryDto>>(
            $"/audit-log?documentId={doc.Id}");
        rows.ShouldNotBeNull();
        rows.ShouldContain(r => r.Action == "RenderPage.BadSignature" && r.IpAddress == forwardedIp);
    }

    private static async Task<DocumentDto> UploadAsync(HttpClient client, string title)
    {
        using var form = new MultipartFormDataContent();
        var pdf = new ByteArrayContent(DocumentViewerApiFactory.FakePdfBytes(title));
        pdf.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(pdf, "file", $"{title.Replace(' ', '_')}.pdf");
        form.Add(new StringContent(title), "Title");
        var response = await client.PostAsync("/documents", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DocumentDto>())!;
    }
}
