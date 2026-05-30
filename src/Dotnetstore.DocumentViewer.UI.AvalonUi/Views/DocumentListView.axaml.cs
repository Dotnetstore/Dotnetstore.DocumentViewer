using Avalonia.Controls;
using Avalonia.Input;
using Dotnetstore.DocumentViewer.UI.AvalonUi.ViewModels;

namespace Dotnetstore.DocumentViewer.UI.AvalonUi.Views;

public partial class DocumentListView : UserControl
{
    public DocumentListView()
    {
        InitializeComponent();
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is DocumentListViewModel vm && vm.SelectedDocument is not null)
            vm.OpenCommand.Execute(vm.SelectedDocument);
    }
}
