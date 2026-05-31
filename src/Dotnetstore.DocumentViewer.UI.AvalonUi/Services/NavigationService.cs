using CommunityToolkit.Mvvm.ComponentModel;
using Dotnetstore.DocumentViewer.UI.AvalonUi.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace Dotnetstore.DocumentViewer.UI.AvalonUi.Services;

internal sealed class NavigationService(IServiceProvider services) : INavigationService
{
    public ObservableObject? CurrentView { get; private set; }
    public event EventHandler? CurrentViewChanged;

    public void NavigateToLogin() => Set(services.GetRequiredService<LoginViewModel>());

    public void NavigateToDocumentList()
    {
        var vm = services.GetRequiredService<DocumentListViewModel>();
        _ = vm.LoadAsync();
        Set(vm);
    }

    public void NavigateToDocument(Guid documentId)
    {
        var vm = services.GetRequiredService<DocumentViewerViewModel>();
        _ = vm.LoadAsync(documentId);
        Set(vm);
    }

    public void NavigateToAdminUsers()
    {
        var vm = services.GetRequiredService<AdminUsersViewModel>();
        _ = vm.LoadAsync();
        Set(vm);
    }

    public void NavigateToAdminAccess()
    {
        var vm = services.GetRequiredService<AdminAccessViewModel>();
        _ = vm.LoadAsync();
        Set(vm);
    }

    public void NavigateToChangePassword() =>
        Set(services.GetRequiredService<ChangePasswordViewModel>());

    public void NavigateToUpload() =>
        Set(services.GetRequiredService<UploadDocumentViewModel>());

    public void NavigateToAllowedIps(Guid documentId)
    {
        var vm = services.GetRequiredService<AllowedIpsViewModel>();
        _ = vm.LoadAsync(documentId);
        Set(vm);
    }

    public void NavigateToDocumentAuditLog(Guid documentId)
    {
        var vm = services.GetRequiredService<DocumentAuditLogViewModel>();
        _ = vm.LoadAsync(documentId);
        Set(vm);
    }

    private void Set(ObservableObject vm)
    {
        CurrentView = vm;
        CurrentViewChanged?.Invoke(this, EventArgs.Empty);
    }
}
