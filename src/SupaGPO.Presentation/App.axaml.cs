using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SupaGPO.Presentation.Services;
using SupaGPO.Presentation.ViewModels;
using SupaGPO.Presentation.Views;

namespace SupaGPO.Presentation;

public partial class App : Application
{
    public static RuntimeHost Host { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        AppLog.Install();
        Host = new RuntimeHost();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = Avalonia.Controls.ShutdownMode.OnMainWindowClose;
            var mainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel()
            };
            desktop.MainWindow = mainWindow;

            // Overlay in-game: ditampilkan SETELAH jendela utama terbuka (Avalonia menolak
            // Show dengan owner yang belum visible), tanpa owner agar tidak ikut minimize.
            _overlayWindow = new Views.OverlayWindow();
            mainWindow.Opened += (_, _) =>
            {
                if (Host.OverlayVisible)
                    _overlayWindow.Show();
            };
            mainWindow.Closed += (_, _) => _overlayWindow.Close();
            Host.OverlayVisibilityChanged += visible => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (visible) _overlayWindow.Show();
                else _overlayWindow.Hide();
            });
        }

        base.OnFrameworkInitializationCompleted();
    }

    private Views.OverlayWindow? _overlayWindow;
}
