using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dotnetstore.DocumentViewer.Shared.SDK;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Auth;
using Dotnetstore.DocumentViewer.UI.AvalonUi.Services;

namespace Dotnetstore.DocumentViewer.UI.AvalonUi.ViewModels;

public sealed partial class ChangePasswordViewModel(
    IDocumentViewerApiClient api,
    IApiSession session,
    INavigationService nav) : ViewModelBase
{
    [ObservableProperty] private string _currentPassword = string.Empty;
    [ObservableProperty] private string _newPassword = string.Empty;
    [ObservableProperty] private string _confirmNewPassword = string.Empty;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;

    public bool MustChangePassword => session.Me?.MustChangePassword ?? false;

    [RelayCommand]
    private async Task Submit()
    {
        ErrorMessage = null;
        StatusMessage = null;

        if (NewPassword.Length < 8)
        {
            ErrorMessage = "New password must be at least 8 characters.";
            return;
        }
        if (NewPassword != ConfirmNewPassword)
        {
            ErrorMessage = "New password and confirmation do not match.";
            return;
        }

        IsBusy = true;
        try
        {
            await api.ChangePasswordAsync(new ChangePasswordRequest(CurrentPassword, NewPassword));
            // Refresh /auth/me so the cached MustChangePassword flag updates.
            var me = await api.MeAsync();
            session.SetMe(me);

            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmNewPassword = string.Empty;
            StatusMessage = "Password changed.";
            // Send the user back to the documents list.
            nav.NavigateToDocumentList();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        // Cancel is only valid when the user isn't being forced to change.
        if (MustChangePassword) return;
        nav.NavigateToDocumentList();
    }
}
