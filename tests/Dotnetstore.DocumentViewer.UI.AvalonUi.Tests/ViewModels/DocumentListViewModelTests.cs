using Dotnetstore.DocumentViewer.Shared.SDK;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Auth;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Documents;
using Dotnetstore.DocumentViewer.UI.AvalonUi.Services;
using Dotnetstore.DocumentViewer.UI.AvalonUi.ViewModels;
using NSubstitute;
using Shouldly;

namespace Dotnetstore.DocumentViewer.UI.AvalonUi.Tests.ViewModels;

public sealed class DocumentListViewModelTests
{
    private readonly IDocumentViewerApiClient _api = Substitute.For<IDocumentViewerApiClient>();
    private readonly IApiSession _session = Substitute.For<IApiSession>();
    private readonly INavigationService _nav = Substitute.For<INavigationService>();

    [Fact]
    public async Task LoadAsync_populates_documents()
    {
        var docs = new List<DocumentDto>
        {
            NewDoc("First"),
            NewDoc("Second"),
        };
        _api.ListDocumentsAsync(Arg.Any<CancellationToken>()).Returns(docs);

        var vm = new DocumentListViewModel(_api, _session, _nav);
        await vm.LoadAsync();

        vm.Documents.Count.ShouldBe(2);
        vm.Documents.Select(d => d.Title).ShouldBe(["First", "Second"]);
        vm.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task LoadAsync_failure_surfaces_error_keeps_documents_empty()
    {
        _api.ListDocumentsAsync(Arg.Any<CancellationToken>()).Returns<Task<IReadOnlyList<DocumentDto>>>(_ =>
            throw new HttpRequestException("network out"));

        var vm = new DocumentListViewModel(_api, _session, _nav);
        await vm.LoadAsync();

        vm.Documents.ShouldBeEmpty();
        vm.ErrorMessage.ShouldBe("network out");
    }

    [Fact]
    public void IsAdmin_reflects_session_state()
    {
        _session.IsAdmin.Returns(true);
        var vm = new DocumentListViewModel(_api, _session, _nav);
        vm.IsAdmin.ShouldBeTrue();
    }

    [Fact]
    public void Open_command_navigates_to_document_view()
    {
        var vm = new DocumentListViewModel(_api, _session, _nav);
        var doc = NewDoc("Pick me");

        vm.OpenCommand.Execute(doc);

        _nav.Received(1).NavigateToDocument(doc.Id);
    }

    [Fact]
    public async Task Delete_command_removes_document_from_list_on_success()
    {
        var doc = NewDoc("Going away");
        _api.ListDocumentsAsync(Arg.Any<CancellationToken>()).Returns(new[] { doc });
        var vm = new DocumentListViewModel(_api, _session, _nav);
        await vm.LoadAsync();
        vm.Documents.ShouldContain(doc);

        await vm.DeleteCommand.ExecuteAsync(doc);

        await _api.Received(1).DeleteDocumentAsync(doc.Id, Arg.Any<CancellationToken>());
        vm.Documents.ShouldNotContain(doc);
    }

    [Fact]
    public async Task Delete_command_surfaces_error_keeps_document()
    {
        var doc = NewDoc("Will fail");
        _api.ListDocumentsAsync(Arg.Any<CancellationToken>()).Returns(new[] { doc });
        _api.DeleteDocumentAsync(doc.Id, Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new HttpRequestException("Forbidden"));
        var vm = new DocumentListViewModel(_api, _session, _nav);
        await vm.LoadAsync();

        await vm.DeleteCommand.ExecuteAsync(doc);

        vm.Documents.ShouldContain(doc);
        vm.ErrorMessage.ShouldBe("Forbidden");
    }

    private static DocumentDto NewDoc(string title) =>
        new(Guid.NewGuid(), title, $"{title}.pdf", "application/pdf",
            PageCount: 1, DocumentStatus.Ready, Guid.NewGuid(), DateTimeOffset.UtcNow);
}
