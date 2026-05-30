using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dotnetstore.DocumentViewer.Shared.SDK;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Users;

namespace Dotnetstore.DocumentViewer.UI.AvalonUi.ViewModels;

public sealed partial class AdminUsersViewModel(IDocumentViewerApiClient api) : ViewModelBase
{
    private const string AdminRole = "Admin";
    private const string ViewerRole = "Viewer";

    public ObservableCollection<UserDto> Users { get; } = [];

    [ObservableProperty] private UserDto? _selectedUser;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;

    // Edit / create form fields. When SelectedUser is null + IsEditing = true → creating a new user.
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _formEmail = string.Empty;
    [ObservableProperty] private string _formDisplayName = string.Empty;
    [ObservableProperty] private string _formPassword = string.Empty;
    [ObservableProperty] private bool _formIsAdmin;
    [ObservableProperty] private bool _formIsViewer = true;

    public bool IsCreatingNew => IsEditing && SelectedUser is null;

    public async Task LoadAsync(CancellationToken ct = default)
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var rows = await api.ListUsersAsync(ct);
            Users.Clear();
            foreach (var u in rows) Users.Add(u);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally { IsBusy = false; }
    }

    partial void OnSelectedUserChanged(UserDto? value)
    {
        if (value is null)
        {
            ResetForm();
            IsEditing = false;
            return;
        }
        FormEmail = value.Email;
        FormDisplayName = value.DisplayName;
        FormPassword = string.Empty;
        FormIsAdmin = value.Roles.Contains(AdminRole);
        FormIsViewer = value.Roles.Contains(ViewerRole);
        IsEditing = true;
        OnPropertyChanged(nameof(IsCreatingNew));
    }

    partial void OnIsEditingChanged(bool value) => OnPropertyChanged(nameof(IsCreatingNew));

    [RelayCommand]
    private void New()
    {
        SelectedUser = null;
        ResetForm();
        IsEditing = true;
        OnPropertyChanged(nameof(IsCreatingNew));
    }

    [RelayCommand]
    private void Cancel()
    {
        SelectedUser = null;
        ResetForm();
        IsEditing = false;
    }

    [RelayCommand]
    private async Task Save()
    {
        if (!IsEditing) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var roles = BuildRoles();
            if (roles.Count == 0)
            {
                ErrorMessage = "Select at least one role.";
                return;
            }

            if (SelectedUser is null)
            {
                var created = await api.CreateUserAsync(new CreateUserRequest(FormEmail, FormDisplayName, FormPassword, roles));
                Users.Add(created);
                SelectedUser = created;
            }
            else
            {
                var updated = await api.UpdateUserAsync(SelectedUser.Id,
                    new UpdateUserRequest(FormDisplayName, roles));
                var idx = Users.IndexOf(SelectedUser);
                if (idx >= 0) Users[idx] = updated;
                SelectedUser = updated;
            }
            IsEditing = false;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task Delete()
    {
        if (SelectedUser is null) return;
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await api.DeleteUserAsync(SelectedUser.Id);
            Users.Remove(SelectedUser);
            SelectedUser = null;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private Task Refresh() => LoadAsync();

    private void ResetForm()
    {
        FormEmail = string.Empty;
        FormDisplayName = string.Empty;
        FormPassword = string.Empty;
        FormIsAdmin = false;
        FormIsViewer = true;
    }

    private IReadOnlyList<string> BuildRoles()
    {
        var roles = new List<string>(2);
        if (FormIsAdmin) roles.Add(AdminRole);
        if (FormIsViewer) roles.Add(ViewerRole);
        return roles;
    }
}
