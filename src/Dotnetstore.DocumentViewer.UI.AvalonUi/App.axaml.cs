using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using Dotnetstore.DocumentViewer.Shared.SDK;
using Dotnetstore.DocumentViewer.UI.AvalonUi.Services;
using Dotnetstore.DocumentViewer.UI.AvalonUi.ViewModels;
using Dotnetstore.DocumentViewer.UI.AvalonUi.Views;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dotnetstore.DocumentViewer.UI.AvalonUi;

public partial class App : Application
{
    public IServiceProvider? Services { get; private set; }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            DisableAvaloniaDataAnnotationValidation();

            var configuration = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddDocumentViewerSdk(configuration);
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<MainWindowViewModel>();
            services.AddTransient<LoginViewModel>();
            services.AddTransient<DocumentListViewModel>();
            services.AddTransient<DocumentViewerViewModel>();
            services.AddTransient<AdminUsersViewModel>();
            services.AddTransient<AdminAccessViewModel>();
            services.AddTransient<AllowedIpsViewModel>();
            services.AddTransient<DocumentAuditLogViewModel>();
            services.AddTransient<ChangePasswordViewModel>();
            services.AddTransient<UploadDocumentViewModel>();

            Services = services.BuildServiceProvider();

            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainWindowViewModel>(),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void DisableAvaloniaDataAnnotationValidation()
    {
        var toRemove = BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();
        foreach (var plugin in toRemove)
            BindingPlugins.DataValidators.Remove(plugin);
    }
}
