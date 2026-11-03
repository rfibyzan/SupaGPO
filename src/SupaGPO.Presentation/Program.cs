using Avalonia;
using SupaGPO.Presentation.Services;
using SupaGPO.Core.Diagnostics;

namespace SupaGPO.Presentation;

class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            FileLog.Line("[SupaGPO] Application start.");
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            FileLog.Error("Program.Main", ex);
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
