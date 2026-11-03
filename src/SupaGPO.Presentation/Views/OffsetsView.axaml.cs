using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SupaGPO.Presentation.Views;

public partial class OffsetsView : UserControl
{
    public OffsetsView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
