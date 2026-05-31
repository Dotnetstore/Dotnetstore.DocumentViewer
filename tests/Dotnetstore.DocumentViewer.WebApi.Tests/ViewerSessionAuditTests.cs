using System.Net;
using System.Net.Http.Json;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Access;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Audit;
using Shouldly;

namespace Dotnetstore.DocumentViewer.WebApi.Tests;

[Collection(nameof(DocumentViewerApiCollection))]
public sealed class ViewerSessionAuditTests(DocumentViewerApiFactory factory)
{
    [Fact]
    public async Task Viewer_session_for_unknown_document_writes_notfound_audit()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var unknownId = Guid.NewGuid();

        var response = await admin.GetAsync($"/documents/{unknownId}/viewer-session");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var rows = await admin.GetFromJsonAsync<List<AuditLogEntryDto>>(
            $"/audit-log?documentId={unknownId}");
        rows.ShouldNotBeNull();
        rows.ShouldContain(r => r.Action == "ViewerSession.NotFound"
                                && r.DocumentId == unknownId
                                && r.ResultCode == 404);
    }

    [Fact]
    public async Task Viewer_session_forbidden_writes_audit_with_viewer_email()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var doc = await DocumentViewerApiFactory.UploadPdfAsync(admin, "VS forbidden audit");
        // Viewer has NO grant for this document — ACL denies before IP policy runs.
        var email = $"vs-fb-{Guid.NewGuid():N}@dotnetstore.test";
        await factory.CreateViewerAsync(email);
        using var viewer = await factory.CreateViewerClientAsync(email);

        var response = await viewer.GetAsync($"/documents/{doc.Id}/viewer-session");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var rows = await admin.GetFromJsonAsync<List<AuditLogEntryDto>>(
            $"/audit-log?documentId={doc.Id}");
        rows.ShouldNotBeNull();
        rows.ShouldContain(r => r.Action == "ViewerSession.Forbidden"
                                && r.DocumentId == doc.Id
                                && r.UserEmail == email
                                && r.ResultCode == 403);
    }

    [Fact]
    public async Task Viewer_session_ip_blocked_writes_audit()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var doc = await DocumentViewerApiFactory.UploadPdfAsync(admin, "VS ip-blocked audit");
        await factory.SetPageCountAsync(doc.Id, 1);
        // Viewer is granted ACL access but the doc's allow-list only permits a different network.
        await DocumentViewerApiFactory.AddAllowedIpAsync(admin, doc.Id, "203.0.113.0/24");
        var email = $"vs-ipb-{Guid.NewGuid():N}@dotnetstore.test";
        var viewer = await factory.CreateViewerAsync(email);
        (await admin.PostAsJsonAsync($"/documents/{doc.Id}/access", new GrantAccessRequest(viewer.Id)))
            .EnsureSuccessStatusCode();
        using var viewerClient = await factory.CreateViewerClientAsync(email);

        using var req = new HttpRequestMessage(HttpMethod.Get, $"/documents/{doc.Id}/viewer-session");
        req.Headers.Add("X-Forwarded-For", "198.51.100.42");
        var response = await viewerClient.SendAsync(req);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var rows = await admin.GetFromJsonAsync<List<AuditLogEntryDto>>(
            $"/audit-log?documentId={doc.Id}");
        rows.ShouldNotBeNull();
        rows.ShouldContain(r => r.Action == "ViewerSession.IpBlocked"
                                && r.DocumentId == doc.Id
                                && r.UserEmail == email
                                && r.IpAddress == "198.51.100.42");
    }
}
