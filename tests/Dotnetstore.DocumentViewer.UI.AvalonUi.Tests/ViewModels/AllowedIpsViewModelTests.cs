using Dotnetstore.DocumentViewer.Shared.SDK;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Access;
using Dotnetstore.DocumentViewer.UI.AvalonUi.Services;
using Dotnetstore.DocumentViewer.UI.AvalonUi.ViewModels;
using NSubstitute;
using Shouldly;

namespace Dotnetstore.DocumentViewer.UI.AvalonUi.Tests.ViewModels;

public sealed class AllowedIpsViewModelTests
{
    private readonly IDocumentViewerApiClient _api = Substitute.For<IDocumentViewerApiClient>();
    private readonly INavigationService _nav = Substitute.For<INavigationService>();

    [Fact]
    public async Task LoadAsync_populates_entries_and_stores_document_id()
    {
        var documentId = Guid.NewGuid();
        _api.ListAllowedIpsAsync(documentId, Arg.Any<CancellationToken>())
            .Returns([NewEntry("10.0.0.0/8"), NewEntry("203.0.113.0/24")]);

        var vm = new AllowedIpsViewModel(_api, _nav);
        await vm.LoadAsync(documentId);

        vm.DocumentId.ShouldBe(documentId);
        vm.Entries.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AddCommand_calls_api_and_inserts_at_top()
    {
        var documentId = Guid.NewGuid();
        var added = NewEntry("192.0.2.0/24", "Office");
        _api.AddAllowedIpAsync(documentId, Arg.Any<AddAllowedIpRequest>(), Arg.Any<CancellationToken>())
            .Returns(added);

        var vm = new AllowedIpsViewModel(_api, _nav);
        await vm.LoadAsync(documentId);
        vm.NewCidr = "192.0.2.0/24";
        vm.NewDescription = "Office";

        await vm.AddCommand.ExecuteAsync(null);

        await _api.Received(1).AddAllowedIpAsync(documentId,
            Arg.Is<AddAllowedIpRequest>(r => r.Cidr == "192.0.2.0/24" && r.Description == "Office"),
            Arg.Any<CancellationToken>());
        vm.Entries[0].ShouldBe(added);
        vm.NewCidr.ShouldBeNull();
        vm.NewDescription.ShouldBeNull();
    }

    [Fact]
    public async Task AddCommand_replaces_existing_row_on_duplicate_id()
    {
        var documentId = Guid.NewGuid();
        var existing = NewEntry("10.0.0.0/8");
        _api.ListAllowedIpsAsync(documentId, Arg.Any<CancellationToken>())
            .Returns([existing]);
        _api.AddAllowedIpAsync(documentId, Arg.Any<AddAllowedIpRequest>(), Arg.Any<CancellationToken>())
            .Returns(existing);

        var vm = new AllowedIpsViewModel(_api, _nav);
        await vm.LoadAsync(documentId);
        vm.NewCidr = "10.0.0.0/8";

        await vm.AddCommand.ExecuteAsync(null);

        vm.Entries.Count.ShouldBe(1);
    }

    [Fact]
    public async Task RemoveCommand_calls_api_and_removes_from_collection()
    {
        var documentId = Guid.NewGuid();
        var a = NewEntry("10.0.0.0/8");
        var b = NewEntry("203.0.113.0/24");
        _api.ListAllowedIpsAsync(documentId, Arg.Any<CancellationToken>())
            .Returns([a, b]);

        var vm = new AllowedIpsViewModel(_api, _nav);
        await vm.LoadAsync(documentId);

        await vm.RemoveCommand.ExecuteAsync(a);

        await _api.Received(1).RemoveAllowedIpAsync(documentId, a.Id, Arg.Any<CancellationToken>());
        vm.Entries.ShouldNotContain(a);
        vm.Entries.ShouldContain(b);
    }

    [Fact]
    public async Task AddCommand_with_blank_cidr_does_nothing()
    {
        var vm = new AllowedIpsViewModel(_api, _nav);
        await vm.LoadAsync(Guid.NewGuid());
        vm.NewCidr = "   ";

        await vm.AddCommand.ExecuteAsync(null);

        await _api.DidNotReceive().AddAllowedIpAsync(Arg.Any<Guid>(), Arg.Any<AddAllowedIpRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void BackCommand_navigates_to_document_list()
    {
        var vm = new AllowedIpsViewModel(_api, _nav);

        vm.BackCommand.Execute(null);

        _nav.Received(1).NavigateToDocumentList();
    }

    private static AllowedIpDto NewEntry(string cidr, string? description = null) =>
        new(Guid.NewGuid(), Guid.NewGuid(), cidr, description, Guid.NewGuid(), DateTimeOffset.UtcNow);
}
