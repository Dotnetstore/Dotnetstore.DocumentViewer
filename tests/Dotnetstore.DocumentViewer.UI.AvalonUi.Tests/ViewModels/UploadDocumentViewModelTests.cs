using Dotnetstore.DocumentViewer.Shared.SDK;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Documents;
using Dotnetstore.DocumentViewer.UI.AvalonUi.Services;
using Dotnetstore.DocumentViewer.UI.AvalonUi.ViewModels;
using NSubstitute;
using Shouldly;

namespace Dotnetstore.DocumentViewer.UI.AvalonUi.Tests.ViewModels;

public sealed class UploadDocumentViewModelTests
{
    private readonly IDocumentViewerApiClient _api = Substitute.For<IDocumentViewerApiClient>();
    private readonly INavigationService _nav = Substitute.For<INavigationService>();

    [Fact]
    public void Upload_command_disabled_until_a_file_is_picked_AND_title_is_set()
    {
        var vm = new UploadDocumentViewModel(_api, _nav);
        vm.UploadCommand.CanExecute(null).ShouldBeFalse();

        vm.SetFile("report.pdf", "application/pdf", size: 1024,
            openStreamAsync: () => Task.FromResult<Stream>(new MemoryStream([1, 2, 3])));

        // SetFile auto-fills Title from the filename so the command should now be enabled.
        vm.HasSelectedFile.ShouldBeTrue();
        vm.Title.ShouldBe("report");
        vm.UploadCommand.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public void Clearing_title_disables_upload_even_with_file_picked()
    {
        var vm = new UploadDocumentViewModel(_api, _nav);
        vm.SetFile("a.pdf", "application/pdf", 1,
            openStreamAsync: () => Task.FromResult<Stream>(new MemoryStream()));

        vm.Title = "   ";
        vm.UploadCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public async Task Upload_calls_api_with_picked_file_then_navigates_to_documents()
    {
        var bytes = new byte[] { 0xAA, 0xBB, 0xCC };
        var vm = new UploadDocumentViewModel(_api, _nav);
        vm.SetFile("report.pdf", "application/pdf", size: bytes.Length,
            openStreamAsync: () => Task.FromResult<Stream>(new MemoryStream(bytes)));
        vm.Title = "Q3 report";

        _api.UploadDocumentAsync("Q3 report", "report.pdf", Arg.Any<Stream>(), "application/pdf",
                Arg.Any<CancellationToken>())
            .Returns(new DocumentDto(Guid.NewGuid(), "Q3 report", "report.pdf", "application/pdf",
                PageCount: 1, DocumentStatus.Ready, Guid.NewGuid(), DateTimeOffset.UtcNow));

        await vm.UploadCommand.ExecuteAsync(null);

        await _api.Received(1).UploadDocumentAsync("Q3 report", "report.pdf", Arg.Any<Stream>(),
            "application/pdf", Arg.Any<CancellationToken>());
        _nav.Received(1).NavigateToDocumentList();
        vm.StatusMessage.ShouldNotBeNullOrEmpty();
        vm.ErrorMessage.ShouldBeNull();
        vm.HasSelectedFile.ShouldBeFalse("file selection cleared after a successful upload");
    }

    [Fact]
    public async Task Upload_failure_surfaces_error_keeps_file_selection_does_not_navigate()
    {
        var vm = new UploadDocumentViewModel(_api, _nav);
        vm.SetFile("doc.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            size: 100, openStreamAsync: () => Task.FromResult<Stream>(new MemoryStream(new byte[100])));
        vm.Title = "Will fail";

        _api.UploadDocumentAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(),
                Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<Task<DocumentDto>>(_ => throw new HttpRequestException("server angry"));

        await vm.UploadCommand.ExecuteAsync(null);

        vm.ErrorMessage.ShouldBe("server angry");
        vm.HasSelectedFile.ShouldBeTrue();
        _nav.DidNotReceive().NavigateToDocumentList();
    }

    [Fact]
    public void ClearFile_resets_picked_state_and_disables_upload()
    {
        var vm = new UploadDocumentViewModel(_api, _nav);
        vm.SetFile("doc.pdf", "application/pdf", 1,
            openStreamAsync: () => Task.FromResult<Stream>(new MemoryStream()));
        vm.UploadCommand.CanExecute(null).ShouldBeTrue();

        vm.ClearFileCommand.Execute(null);

        vm.HasSelectedFile.ShouldBeFalse();
        vm.SelectedFileName.ShouldBe(string.Empty);
        vm.SelectedFileSize.ShouldBe(0);
        vm.UploadCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public void Cancel_returns_to_documents_list()
    {
        var vm = new UploadDocumentViewModel(_api, _nav);
        vm.CancelCommand.Execute(null);
        _nav.Received(1).NavigateToDocumentList();
    }

    [Fact]
    public void SetFile_preserves_an_already_typed_title()
    {
        var vm = new UploadDocumentViewModel(_api, _nav);
        vm.Title = "Custom title";
        vm.SetFile("auto-generated.pdf", "application/pdf", 1,
            openStreamAsync: () => Task.FromResult<Stream>(new MemoryStream()));
        vm.Title.ShouldBe("Custom title");
    }
}
