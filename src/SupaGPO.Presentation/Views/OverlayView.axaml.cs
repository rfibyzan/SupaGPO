using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SupaGPO.Presentation.Views;

public partial class OverlayView : UserControl
{
    public OverlayView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
