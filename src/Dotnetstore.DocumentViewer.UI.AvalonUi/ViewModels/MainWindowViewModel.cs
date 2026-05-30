using CommunityToolkit.Mvvm.ComponentModel;
using Dotnetstore.DocumentViewer.UI.AvalonUi.Services;

namespace Dotnetstore.DocumentViewer.UI.AvalonUi.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _nav;

    [ObservableProperty]
    private ObservableObject? _currentView;

    public MainWindowViewModel(INavigationService nav)
    {
        _nav = nav;
        _nav.CurrentViewChanged += (_, _) => CurrentView = _nav.CurrentView;
        _nav.NavigateToLogin();
    }

    // Parameterless ctor only used by the XAML designer.
    public MainWindowViewModel() : this(new DesignNavigationService()) { }

    private sealed class DesignNavigationService : INavigationService
    {
        public ObservableObject? CurrentView => null;
        public event EventHandler? CurrentViewChanged { add { } remove { } }
        public void NavigateToLogin() { }
        public void NavigateToDocumentList() { }
        public void NavigateToDocument(Guid documentId) { }
    }
}
