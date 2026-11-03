using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SupaGPO.Presentation.Views;

public partial class FishingView : UserControl
{
    public FishingView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
