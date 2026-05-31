using CommunityToolkit.Mvvm.ComponentModel;

namespace Dotnetstore.DocumentViewer.UI.AvalonUi.Services;

public interface INavigationService
{
    ObservableObject? CurrentView { get; }
    event EventHandler? CurrentViewChanged;

    void NavigateToLogin();
    void NavigateToDocumentList();
    void NavigateToDocument(Guid documentId);
    void NavigateToAdminUsers();
    void NavigateToAdminAccess();
    void NavigateToChangePassword();
    void NavigateToUpload();
}
