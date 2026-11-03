using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

namespace SupaGPO.Presentation.Views;

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _uiRefresh = new() { Interval = TimeSpan.FromMilliseconds(250) };

    public MainWindow()
    {
        InitializeComponent();
        // Refresh status realtime (~4 Hz): sebelumnya Refresh() tak pernah dipanggil,
        // status di halaman diam selamanya.
        _uiRefresh.Tick += (_, _) =>
        {
            if (DataContext is ViewModels.MainWindowViewModel vm)
                vm.RefreshCurrent();
        };
        _uiRefresh.Start();
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
