using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Dotnetstore.DocumentViewer.UI.AvalonUi.Views;

public partial class DocumentViewerView : UserControl
{
    public DocumentViewerView()
    {
        InitializeComponent();
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    // Phase A best-effort: swallow common copy/save/print shortcuts within the viewer.
    // Cannot prevent OS-level screenshots; the watermark on the rendered image is the real mitigation.
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            switch (e.Key)
            {
                case Key.C:
                case Key.S:
                case Key.P:
                case Key.X:
                case Key.A:
                    e.Handled = true;
                    return;
            }
        }
        if (e.Key == Key.PrintScreen)
        {
            e.Handled = true;
        }
    }
}
