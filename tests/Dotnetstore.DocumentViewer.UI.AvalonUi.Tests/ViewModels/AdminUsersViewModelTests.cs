using Dotnetstore.DocumentViewer.Shared.SDK;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Users;
using Dotnetstore.DocumentViewer.UI.AvalonUi.ViewModels;
using NSubstitute;
using Shouldly;

namespace Dotnetstore.DocumentViewer.UI.AvalonUi.Tests.ViewModels;

public sealed class AdminUsersViewModelTests
{
    private readonly IDocumentViewerApiClient _api = Substitute.For<IDocumentViewerApiClient>();

    [Fact]
    public async Task LoadAsync_populates_users()
    {
        _api.ListUsersAsync(Arg.Any<CancellationToken>())
            .Returns(new[] { NewUser("alice@x.test"), NewUser("bob@x.test") });

        var vm = new AdminUsersViewModel(_api);
        await vm.LoadAsync();

        vm.Users.Count.ShouldBe(2);
    }

    [Fact]
    public void Selecting_user_fills_edit_form()
    {
        var vm = new AdminUsersViewModel(_api);
        var user = NewUser("alice@x.test", "Alice", ["Admin", "Viewer"]);

        vm.SelectedUser = user;

        vm.IsEditing.ShouldBeTrue();
        vm.IsCreatingNew.ShouldBeFalse();
        vm.FormEmail.ShouldBe("alice@x.test");
        vm.FormDisplayName.ShouldBe("Alice");
        vm.FormIsAdmin.ShouldBeTrue();
        vm.FormIsViewer.ShouldBeTrue();
    }

    [Fact]
    public void New_command_starts_create_mode_with_blank_form()
    {
        var vm = new AdminUsersViewModel(_api);

        vm.NewCommand.Execute(null);

        vm.IsEditing.ShouldBeTrue();
        vm.IsCreatingNew.ShouldBeTrue();
        vm.SelectedUser.ShouldBeNull();
        vm.FormEmail.ShouldBe(string.Empty);
        vm.FormDisplayName.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Save_in_create_mode_calls_CreateUserAsync_and_appends_to_list()
    {
        var vm = new AdminUsersViewModel(_api);
        var created = NewUser("new@x.test", "New", ["Viewer"]);
        _api.CreateUserAsync(Arg.Any<CreateUserRequest>(), Arg.Any<CancellationToken>()).Returns(created);

        vm.NewCommand.Execute(null);
        vm.FormEmail = "new@x.test";
        vm.FormDisplayName = "New";
        vm.FormPassword = "StrongPass123!";
        vm.FormIsViewer = true;
        vm.FormIsAdmin = false;

        await vm.SaveCommand.ExecuteAsync(null);

        await _api.Received(1).CreateUserAsync(
            Arg.Is<CreateUserRequest>(r =>
                r.Email == "new@x.test" &&
                r.DisplayName == "New" &&
                r.Password == "StrongPass123!" &&
                r.Roles.SequenceEqual(new[] { "Viewer" })),
            Arg.Any<CancellationToken>());
        vm.Users.ShouldContain(created);
        vm.IsEditing.ShouldBeFalse();
        vm.ErrorMessage.ShouldBeNull();
    }

    [Fact]
    public async Task Save_in_edit_mode_calls_UpdateUserAsync_and_replaces_row()
    {
        var existing = NewUser("alice@x.test", "Alice", ["Viewer"]);
        var updated = existing with { DisplayName = "Alice Updated", Roles = ["Viewer"] };
        _api.ListUsersAsync(Arg.Any<CancellationToken>()).Returns(new[] { existing });
        _api.UpdateUserAsync(existing.Id, Arg.Any<UpdateUserRequest>(), Arg.Any<CancellationToken>()).Returns(updated);

        var vm = new AdminUsersViewModel(_api);
        await vm.LoadAsync();
        vm.SelectedUser = existing;
        vm.FormDisplayName = "Alice Updated";

        await vm.SaveCommand.ExecuteAsync(null);

        vm.Users.Single().DisplayName.ShouldBe("Alice Updated");
        vm.SelectedUser.ShouldBe(updated);
    }

    [Fact]
    public async Task Save_with_no_roles_selected_surfaces_error_does_not_call_api()
    {
        var vm = new AdminUsersViewModel(_api);
        vm.NewCommand.Execute(null);
        vm.FormEmail = "x@y.z";
        vm.FormDisplayName = "X";
        vm.FormPassword = "StrongPass123!";
        vm.FormIsAdmin = false;
        vm.FormIsViewer = false;

        await vm.SaveCommand.ExecuteAsync(null);

        vm.ErrorMessage.ShouldNotBeNull();
        await _api.DidNotReceive().CreateUserAsync(Arg.Any<CreateUserRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ResetPassword_calls_api_and_records_status_message()
    {
        var user = NewUser("alice@x.test");
        _api.ListUsersAsync(Arg.Any<CancellationToken>()).Returns(new[] { user });
        var vm = new AdminUsersViewModel(_api);
        await vm.LoadAsync();
        vm.SelectedUser = user;
        vm.ResetNewPassword = "BrandNew123!";

        await vm.ResetPasswordCommand.ExecuteAsync(null);

        await _api.Received(1).ResetUserPasswordAsync(user.Id,
            Arg.Is<ResetPasswordRequest>(r => r.NewPassword == "BrandNew123!"),
            Arg.Any<CancellationToken>());
        vm.ResetStatusMessage.ShouldNotBeNullOrEmpty();
        vm.ResetNewPassword.ShouldBe(string.Empty);
    }

    private static UserDto NewUser(string email, string displayName = "User", IReadOnlyList<string>? roles = null) =>
        new(Guid.NewGuid(), email, displayName, roles ?? ["Viewer"], MustChangePassword: false);
}
