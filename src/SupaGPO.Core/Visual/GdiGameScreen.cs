using System.Runtime.InteropServices;
using SupaGPO.Core.Interop;

namespace SupaGPO.Core.Visual;

/// <summary>
/// Capture warna area client jendela Roblox lewat GDI (BitBlt layar → GetDIBits top-down BGRA).
/// Jendela harus terlihat (macro hanya jalan saat Roblox foreground). Tidak pernah throw.
/// </summary>
public sealed class GdiGameScreen : IGameScreen
{
    public const int MaxDimension = 8192;
    private int _pid;
    private nint _hwnd;

    public nint Handle => _hwnd;

    public PixelRect? GetClientRect(int processId)
    {
        try
        {
            if (processId <= 0) return null;
            if (_pid != processId || _hwnd == 0 || !IsWindow(_hwnd))
            {
                using var p = System.Diagnostics.Process.GetProcessById(processId);
                _hwnd = p.MainWindowHandle;
                _pid = processId;
            }
            if (_hwnd == 0 || IsIconic(_hwnd)) return null;
            if (!GetClientRectNative(_hwnd, out var rc)) return null;
            var origin = new NativePoint();
            if (!ClientToScreen(_hwnd, ref origin)) return null;
            int w = rc.Right - rc.Left, h = rc.Bottom - rc.Top;
            return w > 0 && h > 0 ? new PixelRect(origin.X, origin.Y, w, h) : null;
        }
        catch
        {
            _hwnd = 0;
            return null;
        }
    }

    public ColorFrame? Capture(PixelRect r)
    {
        if (r.W <= 0 || r.H <= 0 || r.W > MaxDimension || r.H > MaxDimension) return null;
        nint screenDc = 0, memDc = 0, bmp = 0, oldObj = 0;
        try
        {
            screenDc = NativeMethods.GetDC(0);
            if (screenDc == 0) return null;
            memDc = NativeMethods.CreateCompatibleDC(screenDc);
            if (memDc == 0) return null;
            bmp = NativeMethods.CreateCompatibleBitmap(screenDc, r.W, r.H);
            if (bmp == 0) return null;
            oldObj = NativeMethods.SelectObject(memDc, bmp);
            if (oldObj == 0) return null;
            if (!NativeMethods.BitBlt(memDc, 0, 0, r.W, r.H, screenDc, r.X, r.Y, NativeMethods.Srccopy))
                return null;

            var pixels = new byte[r.W * r.H * 4];
            var info = new BitmapInfo
            {
                bmiHeader = new BitmapInfoHeader
                {
                    biSize = 40,
                    biWidth = r.W,
                    biHeight = -r.H, // negatif = top-down
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = 0,
                    biSizeImage = pixels.Length,
                },
                bmiColors = new uint[256],
            };
            if (NativeMethods.GetDIBits(memDc, bmp, 0, (uint)r.H, pixels, ref info, NativeMethods.DiRgbColors) == 0)
                return null;
            return new ColorFrame(r.W, r.H, pixels);
        }
        catch
        {
            return null;
        }
        finally
        {
            if (memDc != 0)
            {
                if (oldObj != 0) NativeMethods.SelectObject(memDc, oldObj);
                NativeMethods.DeleteDC(memDc);
            }
            if (bmp != 0) NativeMethods.DeleteObject(bmp);
            if (screenDc != 0) NativeMethods.ReleaseDC(0, screenDc);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint { public int X, Y; }

    [DllImport("user32.dll", EntryPoint = "GetClientRect")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetClientRectNative(nint hWnd, out NativeRect rect);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ClientToScreen(nint hWnd, ref NativePoint point);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindow(nint hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(nint hWnd);
}
