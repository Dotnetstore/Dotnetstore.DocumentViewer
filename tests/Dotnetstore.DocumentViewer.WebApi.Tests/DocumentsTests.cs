using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Access;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Documents;
using Shouldly;

namespace Dotnetstore.DocumentViewer.WebApi.Tests;

[Collection(nameof(DocumentViewerApiCollection))]
public sealed class DocumentsTests(DocumentViewerApiFactory factory)
{
    [Fact]
    public async Task Admin_can_upload_pdf()
    {
        using var admin = await factory.CreateAdminClientAsync();

        var dto = await UploadAsync(admin, "Sample upload");

        dto.Title.ShouldBe("Sample upload");
        dto.ContentType.ShouldBe("application/pdf");
        dto.Status.ShouldBe(DocumentStatus.Ready);
    }

    [Fact]
    public async Task Viewer_cannot_upload()
    {
        var email = $"upl-viewer-{Guid.NewGuid():N}@dotnetstore.test";
        await factory.CreateViewerAsync(email);
        using var viewer = await factory.CreateViewerClientAsync(email);

        using var form = BuildForm("Should fail");
        var response = await viewer.PostAsync("/documents", form);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Upload_with_non_pdf_content_type_returns_415()
    {
        using var admin = await factory.CreateAdminClientAsync();

        using var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(DocumentViewerApiFactory.FakePdfBytes());
        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        form.Add(content, "file", "doc.txt");

        var response = await admin.PostAsync("/documents", form);
        response.StatusCode.ShouldBe(HttpStatusCode.UnsupportedMediaType);
    }

    [Fact]
    public async Task Admin_sees_all_documents_viewer_only_granted()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var ungranted = await UploadAsync(admin, "Ungranted to viewer");
        var granted = await UploadAsync(admin, "Granted to viewer");

        var email = $"list-viewer-{Guid.NewGuid():N}@dotnetstore.test";
        var viewer = await factory.CreateViewerAsync(email);
        var grant = await admin.PostAsJsonAsync($"/documents/{granted.Id}/access",
            new GrantAccessRequest(viewer.Id));
        grant.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var viewerClient = await factory.CreateViewerClientAsync(email);
        var viewerList = await viewerClient.GetFromJsonAsync<List<DocumentDto>>("/documents");
        viewerList.ShouldNotBeNull();
        viewerList.ShouldContain(d => d.Id == granted.Id);
        viewerList.ShouldNotContain(d => d.Id == ungranted.Id);

        var adminList = await admin.GetFromJsonAsync<List<DocumentDto>>("/documents");
        adminList.ShouldNotBeNull();
        adminList.Select(d => d.Id).ShouldContain(granted.Id);
        adminList.Select(d => d.Id).ShouldContain(ungranted.Id);
    }

    [Fact]
    public async Task GetMetadata_returns_404_when_unknown_and_403_when_ungranted()
    {
        using var admin = await factory.CreateAdminClientAsync();
        var doc = await UploadAsync(admin, "Metadata-test");

        var email = $"meta-viewer-{Guid.NewGuid():N}@dotnetstore.test";
        await factory.CreateViewerAsync(email);
        using var viewer = await factory.CreateViewerClientAsync(email);

        var notFound = await viewer.GetAsync($"/documents/{Guid.NewGuid()}");
        notFound.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var forbidden = await viewer.GetAsync($"/documents/{doc.Id}");
        forbidden.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static async Task<DocumentDto> UploadAsync(HttpClient client, string title)
    {
        using var form = BuildForm(title);
        var response = await client.PostAsync("/documents", form);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<DocumentDto>())!;
    }

    private static MultipartFormDataContent BuildForm(string title)
    {
        var form = new MultipartFormDataContent();
        var pdf = new ByteArrayContent(DocumentViewerApiFactory.FakePdfBytes(title));
        pdf.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(pdf, "file", $"{title.Replace(' ', '_')}.pdf");
        form.Add(new StringContent(title), "Title");
        return form;
    }
}
