using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dotnetstore.DocumentViewer.Shared.SDK;
using Dotnetstore.DocumentViewer.Shared.SDK.Dtos.Auth;
using Dotnetstore.DocumentViewer.UI.AvalonUi.Services;

namespace Dotnetstore.DocumentViewer.UI.AvalonUi.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _nav;
    private readonly IApiSession _session;
    private readonly IDocumentViewerApiClient? _api;

    [ObservableProperty] private ObservableObject? _currentView;

    public bool IsAuthenticated => _session.IsAuthenticated;
    public bool IsAdmin => _session.IsAdmin;
    public string? CurrentUserEmail => _session.Me?.Email;
    public string? CurrentClientIp => _session.Me?.ClientIp;
    public string CopyrightLine => $"© Dotnetstore {DateTime.UtcNow.Year}";

    public MainWindowViewModel(INavigationService nav, IApiSession session, IDocumentViewerApiClient api)
    {
        _nav = nav;
        _session = session;
        _api = api;
        _nav.CurrentViewChanged += (_, _) => CurrentView = _nav.CurrentView;
        _session.Changed += (_, _) =>
        {
            OnPropertyChanged(nameof(IsAuthenticated));
            OnPropertyChanged(nameof(IsAdmin));
            OnPropertyChanged(nameof(CurrentUserEmail));
            OnPropertyChanged(nameof(CurrentClientIp));
        };
        _nav.NavigateToLogin();
    }

    // Parameterless ctor only used by the XAML designer.
    public MainWindowViewModel() : this(new DesignNavigationService(), new InMemoryApiSession()) { }

    // Designer-only overload — the api client is unavailable at design time.
    private MainWindowViewModel(INavigationService nav, IApiSession session)
    {
        _nav = nav;
        _session = session;
        _api = null;
    }

    [RelayCommand] private void GoToDocuments() => _nav.NavigateToDocumentList();
    [RelayCommand] private void GoToUpload() => _nav.NavigateToUpload();
    [RelayCommand] private void GoToAdminUsers() => _nav.NavigateToAdminUsers();
    [RelayCommand] private void GoToAdminAccess() => _nav.NavigateToAdminAccess();
    [RelayCommand] private void GoToChangePassword() => _nav.NavigateToChangePassword();

    [RelayCommand]
    private async Task SignOut()
    {
        // Best-effort: revoke the refresh token server-side so even a leaked token is dead.
        // Failures shouldn't keep the user logged in locally, so swallow them.
        var token = _session.RefreshToken;
        if (_api is not null && !string.IsNullOrWhiteSpace(token))
        {
            try { await _api.LogoutAsync(new LogoutRequest(token)); }
            catch { /* server-side revoke is best-effort */ }
        }
        _session.Clear();
        _nav.NavigateToLogin();
    }

    private sealed class DesignNavigationService : INavigationService
    {
        public ObservableObject? CurrentView => null;
        public event EventHandler? CurrentViewChanged { add { } remove { } }
        public void NavigateToLogin() { }
        public void NavigateToDocumentList() { }
        public void NavigateToDocument(Guid documentId) { }
        public void NavigateToAdminUsers() { }
        public void NavigateToAdminAccess() { }
        public void NavigateToChangePassword() { }
        public void NavigateToUpload() { }
        public void NavigateToAllowedIps(Guid documentId) { }
        public void NavigateToDocumentAuditLog(Guid documentId) { }
    }
}
