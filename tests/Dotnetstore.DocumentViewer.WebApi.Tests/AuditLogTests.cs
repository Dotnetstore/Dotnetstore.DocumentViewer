using System.Net;
using System.Net.Http.Json;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Audit;
using Shouldly;

namespace Dotnetstore.DocumentViewer.WebApi.Tests;

[Collection(nameof(DocumentViewerApiCollection))]
public sealed class AuditLogTests(DocumentViewerApiFactory factory)
{
    [Fact]
    public async Task Admin_can_query_audit_log_after_failed_render_attempts()
    {
        using var admin = await factory.CreateAdminClientAsync();

        // Upload a doc and try a render with no signature so we have a known audit row to look for.
        var doc = await DocumentViewerApiFactory.UploadPdfAsync(admin, "Audit subject");
        var unsigned = await admin.GetAsync($"/documents/{doc.Id}/pages/0");
        unsigned.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var rows = await admin.GetFromJsonAsync<List<AuditLogEntryDto>>(
            $"/audit-log?documentId={doc.Id}&take=50");
        rows.ShouldNotBeNull();
        rows.ShouldContain(r => r.Action == "RenderPage.BadSignature" && r.DocumentId == doc.Id);
    }

    [Fact]
    public async Task Audit_log_filters_by_action_prefix()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var doc = await DocumentViewerApiFactory.UploadPdfAsync(admin, "Audit filter");
        _ = await admin.GetAsync($"/documents/{doc.Id}/pages/0");

        var rows = await admin.GetFromJsonAsync<List<AuditLogEntryDto>>(
            $"/audit-log?action=RenderPage&take=50");
        rows.ShouldNotBeNull();
        rows.ShouldAllBe(r => r.Action.StartsWith("RenderPage"));
    }

    [Fact]
    public async Task Viewer_cannot_query_audit_log()
    {
        var email = $"audit-viewer-{Guid.NewGuid():N}@dotnetstore.test";
        await factory.CreateViewerAsync(email);
        using var viewer = await factory.CreateViewerClientAsync(email);

        var response = await viewer.GetAsync("/audit-log");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Audit_log_rows_include_user_email_via_left_join()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var doc = await DocumentViewerApiFactory.UploadPdfAsync(admin, "Audit user-email");
        // The Upload itself wrote a DocumentUploaded audit row for the admin; the
        // LEFT JOIN in QueryAuditLogEndpoint should resolve UserId -> email.
        var rows = await admin.GetFromJsonAsync<List<AuditLogEntryDto>>(
            $"/audit-log?documentId={doc.Id}&take=50");
        rows.ShouldNotBeNull();
        rows.ShouldContain(r => r.DocumentId == doc.Id && r.UserEmail == DocumentViewerApiFactory.AdminEmail);
    }
}
