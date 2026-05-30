using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dotnetstore.DocumentViewer.Shared.SDK;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Auth;
using Dotnetstore.DocumentViewer.UI.AvalonUi.Services;

namespace Dotnetstore.DocumentViewer.UI.AvalonUi.ViewModels;

public sealed partial class LoginViewModel(
    IDocumentViewerApiClient api,
    IApiSession session,
    INavigationService nav) : ViewModelBase
{
    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _isBusy;

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            var tokens = await api.LoginAsync(new LoginRequest(Email, Password));
            session.Set(tokens.AccessToken, tokens.AccessTokenExpiresAt, tokens.RefreshToken, tokens.RefreshTokenExpiresAt);
            // Fetch /auth/me so the shell knows the user's roles before navigating.
            var me = await api.MeAsync();
            session.SetMe(me);
            Password = string.Empty;
            if (me.MustChangePassword)
                nav.NavigateToChangePassword();
            else
                nav.NavigateToDocumentList();
        }
        catch (HttpRequestException ex)
        {
            ErrorMessage = $"Login failed: {ex.StatusCode?.ToString() ?? ex.Message}";
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

    private bool CanLogin() => !IsBusy && Email.Length > 0 && Password.Length > 0;

    partial void OnEmailChanged(string value) => LoginCommand.NotifyCanExecuteChanged();
    partial void OnPasswordChanged(string value) => LoginCommand.NotifyCanExecuteChanged();
    partial void OnIsBusyChanged(bool value) => LoginCommand.NotifyCanExecuteChanged();
}
