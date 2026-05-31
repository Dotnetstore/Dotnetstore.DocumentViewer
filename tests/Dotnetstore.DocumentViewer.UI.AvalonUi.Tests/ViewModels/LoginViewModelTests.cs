using Dotnetstore.DocumentViewer.Shared.SDK;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Auth;
using Dotnetstore.DocumentViewer.UI.AvalonUi.Services;
using Dotnetstore.DocumentViewer.UI.AvalonUi.ViewModels;
using NSubstitute;
using Shouldly;

namespace Dotnetstore.DocumentViewer.UI.AvalonUi.Tests.ViewModels;

public sealed class LoginViewModelTests
{
    private readonly IDocumentViewerApiClient _api = Substitute.For<IDocumentViewerApiClient>();
    private readonly IApiSession _session = Substitute.For<IApiSession>();
    private readonly INavigationService _nav = Substitute.For<INavigationService>();

    [Fact]
    public void Login_command_disabled_until_email_and_password_filled()
    {
        var vm = new LoginViewModel(_api, _session, _nav);

        vm.LoginCommand.CanExecute(null).ShouldBeFalse();

        vm.Email = "alice@dotnetstore.test";
        vm.LoginCommand.CanExecute(null).ShouldBeFalse();

        vm.Password = "secret";
        vm.LoginCommand.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public async Task Successful_login_stores_tokens_then_navigates_to_document_list()
    {
        var vm = new LoginViewModel(_api, _session, _nav);
        var tokens = new TokenResponse("acc", DateTimeOffset.UtcNow.AddMinutes(15), "ref", DateTimeOffset.UtcNow.AddDays(14));
        var me = new MeResponse(Guid.NewGuid(), "alice@dotnetstore.test", "Alice",
            ["Viewer"], MustChangePassword: false);
        _api.LoginAsync(Arg.Any<LoginRequest>()).Returns(tokens);
        _api.MeAsync().Returns(me);

        vm.Email = "alice@dotnetstore.test";
        vm.Password = "secret";
        await vm.LoginCommand.ExecuteAsync(null);

        _session.Received(1).Set("acc", tokens.AccessTokenExpiresAt, "ref", tokens.RefreshTokenExpiresAt);
        _session.Received(1).SetMe(me);
        _nav.Received(1).NavigateToDocumentList();
        _nav.DidNotReceive().NavigateToChangePassword();
        vm.Password.ShouldBe(string.Empty);
        vm.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task Login_with_must_change_password_routes_to_change_password_screen()
    {
        var vm = new LoginViewModel(_api, _session, _nav);
        var tokens = new TokenResponse("acc", DateTimeOffset.UtcNow.AddMinutes(15), "ref", DateTimeOffset.UtcNow.AddDays(14));
        var me = new MeResponse(Guid.NewGuid(), "admin@dotnetstore.test", "Admin",
            ["Admin"], MustChangePassword: true);
        _api.LoginAsync(Arg.Any<LoginRequest>()).Returns(tokens);
        _api.MeAsync().Returns(me);

        vm.Email = "admin@dotnetstore.test";
        vm.Password = "ChangeMe123!";
        await vm.LoginCommand.ExecuteAsync(null);

        _nav.Received(1).NavigateToChangePassword();
        _nav.DidNotReceive().NavigateToDocumentList();
    }

    [Fact]
    public async Task Login_failure_shows_message_keeps_password()
    {
        var vm = new LoginViewModel(_api, _session, _nav);
        _api.LoginAsync(Arg.Any<LoginRequest>()).Returns<Task<TokenResponse>>(_ => throw new InvalidOperationException("boom"));

        vm.Email = "x@y.z";
        vm.Password = "wrong";
        await vm.LoginCommand.ExecuteAsync(null);

        vm.ErrorMessage.ShouldBe("boom");
        vm.Password.ShouldBe("wrong");
        _nav.DidNotReceive().NavigateToDocumentList();
        _nav.DidNotReceive().NavigateToChangePassword();
    }
}
