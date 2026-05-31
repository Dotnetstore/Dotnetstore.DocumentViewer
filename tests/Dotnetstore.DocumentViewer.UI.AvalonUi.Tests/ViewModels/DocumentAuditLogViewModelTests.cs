using Dotnetstore.DocumentViewer.Shared.SDK;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Audit;
using Dotnetstore.DocumentViewer.UI.AvalonUi.Services;
using Dotnetstore.DocumentViewer.UI.AvalonUi.ViewModels;
using NSubstitute;
using Shouldly;

namespace Dotnetstore.DocumentViewer.UI.AvalonUi.Tests.ViewModels;

public sealed class DocumentAuditLogViewModelTests
{
    private readonly IDocumentViewerApiClient _api = Substitute.For<IDocumentViewerApiClient>();
    private readonly INavigationService _nav = Substitute.For<INavigationService>();

    [Fact]
    public async Task LoadAsync_populates_entries_and_filters_by_document_id()
    {
        var documentId = Guid.NewGuid();
        _api.QueryAuditLogAsync(
                Arg.Is<AuditLogQuery>(q => q.DocumentId == documentId),
                Arg.Any<CancellationToken>())
            .Returns([NewRow("RenderPage"), NewRow("RenderPage.IpBlocked")]);

        var vm = new DocumentAuditLogViewModel(_api, _nav);
        await vm.LoadAsync(documentId);

        vm.DocumentId.ShouldBe(documentId);
        vm.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RefreshCommand_re_queries_the_api()
    {
        var documentId = Guid.NewGuid();
        _api.QueryAuditLogAsync(Arg.Any<AuditLogQuery>(), Arg.Any<CancellationToken>())
            .Returns([NewRow("RenderPage")]);

        var vm = new DocumentAuditLogViewModel(_api, _nav);
        await vm.LoadAsync(documentId);

        await vm.RefreshCommand.ExecuteAsync(null);

        await _api.Received(2).QueryAuditLogAsync(
            Arg.Is<AuditLogQuery>(q => q.DocumentId == documentId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void BackCommand_navigates_to_document_list()
    {
        var vm = new DocumentAuditLogViewModel(_api, _nav);

        vm.BackCommand.Execute(null);

        _nav.Received(1).NavigateToDocumentList();
    }

    private static AuditLogEntryDto NewRow(string action, string? userEmail = "user@x.test") => new(
        Guid.NewGuid(),
        UserId: Guid.NewGuid(),
        DocumentId: Guid.NewGuid(),
        PageNumber: 0,
        Action: action,
        ResultCode: 200,
        IpAddress: "127.0.0.1",
        AtUtc: DateTimeOffset.UtcNow,
        UserEmail: userEmail);
}
