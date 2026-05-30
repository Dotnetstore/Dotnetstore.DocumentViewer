using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dotnetstore.DocumentViewer.Shared.SDK;
using Dotnetstore.DocumentViewer.UI.AvalonUi.Services;

namespace Dotnetstore.DocumentViewer.UI.AvalonUi.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _nav;
    private readonly IApiSession _session;

    [ObservableProperty] private ObservableObject? _currentView;

    public bool IsAuthenticated => _session.IsAuthenticated;
    public bool IsAdmin => _session.IsAdmin;
    public string? CurrentUserEmail => _session.Me?.Email;

    public MainWindowViewModel(INavigationService nav, IApiSession session)
    {
        _nav = nav;
        _session = session;
        _nav.CurrentViewChanged += (_, _) => CurrentView = _nav.CurrentView;
        _session.Changed += (_, _) =>
        {
            OnPropertyChanged(nameof(IsAuthenticated));
            OnPropertyChanged(nameof(IsAdmin));
            OnPropertyChanged(nameof(CurrentUserEmail));
        };
        _nav.NavigateToLogin();
    }

    // Parameterless ctor only used by the XAML designer.
    public MainWindowViewModel() : this(new DesignNavigationService(), new InMemoryApiSession()) { }

    [RelayCommand] private void GoToDocuments() => _nav.NavigateToDocumentList();
    [RelayCommand] private void GoToAdminUsers() => _nav.NavigateToAdminUsers();
    [RelayCommand] private void GoToAdminAccess() => _nav.NavigateToAdminAccess();

    [RelayCommand]
    private void SignOut()
    {
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
    }
}
