using Dotnetstore.DocumentViewer.Shared.SDK;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Auth;
using Dotnetstore.DocumentViewer.UI.AvalonUi.Services;
using Dotnetstore.DocumentViewer.UI.AvalonUi.ViewModels;
using NSubstitute;
using Shouldly;

namespace Dotnetstore.DocumentViewer.UI.AvalonUi.Tests.ViewModels;

public sealed class MainWindowViewModelTests
{
    private readonly INavigationService _nav = Substitute.For<INavigationService>();
    private readonly IApiSession _session = Substitute.For<IApiSession>();
    private readonly IDocumentViewerApiClient _api = Substitute.For<IDocumentViewerApiClient>();

    [Fact]
    public void Constructor_routes_to_login()
    {
        _ = new MainWindowViewModel(_nav, _session, _api);
        _nav.Received(1).NavigateToLogin();
    }

    [Fact]
    public void Session_Changed_event_raises_IsAuthenticated_IsAdmin_email_PropertyChanged()
    {
        _session.IsAuthenticated.Returns(true);
        _session.IsAdmin.Returns(true);
        _session.Me.Returns(new MeResponse(Guid.NewGuid(), "admin@x.test", "Admin",
            ["Admin"], MustChangePassword: false, ClientIp: "10.0.0.42"));
        var vm = new MainWindowViewModel(_nav, _session, _api);

        var changes = new List<string>();
        vm.PropertyChanged += (_, e) => changes.Add(e.PropertyName ?? string.Empty);

        _session.Changed += Raise.EventWith(_session, EventArgs.Empty);

        changes.ShouldContain(nameof(MainWindowViewModel.IsAuthenticated));
        changes.ShouldContain(nameof(MainWindowViewModel.IsAdmin));
        changes.ShouldContain(nameof(MainWindowViewModel.CurrentUserEmail));
        changes.ShouldContain(nameof(MainWindowViewModel.CurrentClientIp));
        vm.IsAuthenticated.ShouldBeTrue();
        vm.IsAdmin.ShouldBeTrue();
        vm.CurrentUserEmail.ShouldBe("admin@x.test");
        vm.CurrentClientIp.ShouldBe("10.0.0.42");
        vm.CopyrightLine.ShouldContain("Dotnetstore");
    }

    [Fact]
    public void Navigation_commands_delegate_to_INavigationService()
    {
        var vm = new MainWindowViewModel(_nav, _session, _api);

        vm.GoToDocumentsCommand.Execute(null);
        vm.GoToAdminUsersCommand.Execute(null);
        vm.GoToAdminAccessCommand.Execute(null);
        vm.GoToChangePasswordCommand.Execute(null);

        _nav.Received(1).NavigateToDocumentList();
        _nav.Received(1).NavigateToAdminUsers();
        _nav.Received(1).NavigateToAdminAccess();
        _nav.Received(1).NavigateToChangePassword();
    }

    [Fact]
    public async Task SignOut_calls_LogoutAsync_with_refresh_token_then_clears_session_and_navigates()
    {
        _session.RefreshToken.Returns("rt-123");
        var vm = new MainWindowViewModel(_nav, _session, _api);
        _nav.ClearReceivedCalls();

        await vm.SignOutCommand.ExecuteAsync(null);

        await _api.Received(1).LogoutAsync(
            Arg.Is<LogoutRequest>(r => r.RefreshToken == "rt-123"),
            Arg.Any<CancellationToken>());
        _session.Received(1).Clear();
        _nav.Received(1).NavigateToLogin();
    }

    [Fact]
    public async Task SignOut_swallows_logout_failure_still_clears_session()
    {
        _session.RefreshToken.Returns("rt-123");
        _api.LogoutAsync(Arg.Any<LogoutRequest>(), Arg.Any<CancellationToken>())
            .Returns<Task>(_ => throw new HttpRequestException("server down"));
        var vm = new MainWindowViewModel(_nav, _session, _api);
        _nav.ClearReceivedCalls();

        await vm.SignOutCommand.ExecuteAsync(null);

        _session.Received(1).Clear();
        _nav.Received(1).NavigateToLogin();
    }

    [Fact]
    public async Task SignOut_with_no_refresh_token_skips_api_call()
    {
        _session.RefreshToken.Returns((string?)null);
        var vm = new MainWindowViewModel(_nav, _session, _api);
        _nav.ClearReceivedCalls();

        await vm.SignOutCommand.ExecuteAsync(null);

        await _api.DidNotReceive().LogoutAsync(Arg.Any<LogoutRequest>(), Arg.Any<CancellationToken>());
        _session.Received(1).Clear();
        _nav.Received(1).NavigateToLogin();
    }
}
