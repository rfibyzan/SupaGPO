using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace SupaGPO.Presentation.Views;

/// <summary>
/// Overlay in-game: jendela kecil selalu di atas, click-through (WS_EX_TRANSPARENT),
/// tanpa aktivasi fokus. Konten diisi polling 4 Hz dari macro.
/// </summary>
public partial class OverlayWindow : Window
{
    private readonly DispatcherTimer _timer;

    public OverlayWindow()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _timer.Tick += (_, _) => UpdateContent();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        MakeClickThrough();
        var wa = Screens.Primary?.WorkingArea;
        if (wa is { } area)
        {
            // Posisi: bawah tengah layar (di atas taskbar sedikit).
            Position = new PixelPoint(
                area.X + Math.Max(0, (area.Width - 280) / 2),
                area.Y + Math.Max(0, area.Height - 140));
        }
        _timer.Start();
        UpdateContent();
    }

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        base.OnClosed(e);
    }

    private void UpdateContent()
    {
        var m = App.Host.Macro;
        StatusText.Text = m.StatusText;
        SessionText.Text = m.SessionText;
        ProgressBar.Value = Math.Clamp(m.LastProgress * 100, 0, 100);
    }

    private void MakeClickThrough()
    {
        const int gwlExStyle = -20;
        const int wsExLayered = 0x00080000;
        const int wsExTransparent = 0x00000020;
        const int wsExNoActivate = 0x08000000;
        const int wsExToolWindow = 0x00000080;
        nint handle = TryGetPlatformHandle()?.Handle ?? 0;
        if (handle == 0) return;
        int style = GetWindowLong(handle, gwlExStyle);
        SetWindowLong(handle, gwlExStyle,
            style | wsExLayered | wsExTransparent | wsExNoActivate | wsExToolWindow);
    }

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW")]
    private static extern int SetWindowLong(nint hWnd, int nIndex, int dwNewLong);
}
