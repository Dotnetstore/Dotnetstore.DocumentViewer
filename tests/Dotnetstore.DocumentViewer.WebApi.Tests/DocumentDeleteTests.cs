using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Access;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Audit;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Documents;
using Shouldly;

namespace Dotnetstore.DocumentViewer.WebApi.Tests;

[Collection(nameof(DocumentViewerApiCollection))]
public sealed class DocumentDeleteTests(DocumentViewerApiFactory factory)
{
    [Fact]
    public async Task Admin_can_delete_document_subsequent_get_returns_404()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var doc = await UploadAsync(admin, "To be deleted");

        var delete = await admin.DeleteAsync($"/documents/{doc.Id}");
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var afterGet = await admin.GetAsync($"/documents/{doc.Id}");
        afterGet.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_returns_404_for_unknown_document()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var response = await admin.DeleteAsync($"/documents/{Guid.NewGuid()}");
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Viewer_cannot_delete_document()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var doc = await UploadAsync(admin, "Viewer cannot delete");

        var email = $"del-viewer-{Guid.NewGuid():N}@dotnetstore.test";
        await factory.CreateViewerAsync(email);
        using var viewer = await factory.CreateViewerClientAsync(email);

        var response = await viewer.DeleteAsync($"/documents/{doc.Id}");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Document should still exist.
        var still = await admin.GetAsync($"/documents/{doc.Id}");
        still.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Delete_removes_existing_access_grants()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var doc = await UploadAsync(admin, "Has grants");
        var email = $"del-grantee-{Guid.NewGuid():N}@dotnetstore.test";
        var viewer = await factory.CreateViewerAsync(email);

        var grant = await admin.PostAsJsonAsync($"/documents/{doc.Id}/access",
            new GrantAccessRequest(viewer.Id));
        grant.StatusCode.ShouldBe(HttpStatusCode.OK);

        var delete = await admin.DeleteAsync($"/documents/{doc.Id}");
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // The viewer should no longer see this doc in their list.
        using var viewerClient = await factory.CreateViewerClientAsync(email);
        var viewerList = await viewerClient.GetFromJsonAsync<List<DocumentDto>>("/documents");
        viewerList.ShouldNotBeNull();
        viewerList.ShouldNotContain(d => d.Id == doc.Id);
    }

    [Fact]
    public async Task Delete_writes_DocumentDeleted_audit_row()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var doc = await UploadAsync(admin, "Audited delete");

        var delete = await admin.DeleteAsync($"/documents/{doc.Id}");
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var rows = await admin.GetFromJsonAsync<List<AuditLogEntryDto>>(
            $"/audit-log?documentId={doc.Id}");
        rows.ShouldNotBeNull();
        rows.ShouldContain(r => r.Action == "DocumentDeleted" && r.DocumentId == doc.Id);
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
