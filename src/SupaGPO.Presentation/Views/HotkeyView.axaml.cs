using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SupaGPO.Presentation.Views;

public partial class HotkeyView : UserControl
{
    public HotkeyView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
