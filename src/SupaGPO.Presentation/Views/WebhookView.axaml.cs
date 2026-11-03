using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace SupaGPO.Presentation.Views;

public partial class WebhookView : UserControl
{
    public WebhookView()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
