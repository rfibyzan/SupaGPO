using System.Runtime.InteropServices;

namespace SupaGPO.Core.Interop;

internal static partial class NativeMethods
{
    public const uint ProcessVmRead = 0x0010;
    public const uint ProcessVmWrite = 0x0020;
    public const uint ProcessVmOperation = 0x0008;
    public const uint ProcessQueryInformation = 0x0400;
    public const uint ProcessQueryLimitedInformation = 0x1000;

    public const uint AttachAccess =
        ProcessQueryLimitedInformation | ProcessQueryInformation | ProcessVmRead;
    public const uint WriteAccess = ProcessVmWrite | ProcessVmOperation;

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial nint OpenProcess(uint desiredAccess,
        [MarshalAs(UnmanagedType.Bool)] bool inheritHandle, uint processId);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool ReadProcessMemory(nint process,
        nint baseAddress, [Out] byte[] buffer, nint size, out nint bytesRead);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool WriteProcessMemory(nint process,
        nint baseAddress, byte[] buffer, nint size, out nint bytesWritten);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseHandle(nint handle);

    [LibraryImport("kernel32.dll", EntryPoint = "QueryFullProcessImageNameW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool QueryFullProcessImageName(nint process, uint flags,
        [Out] ushort[] exeName, ref uint size);

    // ===== GDI screen capture (untuk visual reel detection) =====

    public const int Srccopy = 0x00CC0020;
    public const int DiRgbColors = 0;

    [LibraryImport("user32.dll")]
    public static partial nint GetDC(nint hwnd);

    [LibraryImport("user32.dll")]
    public static partial int ReleaseDC(nint hwnd, nint hdc);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    public static partial nint CreateCompatibleDC(nint hdc);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    public static partial nint CreateCompatibleBitmap(nint hdc, int width, int height);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    public static partial nint SelectObject(nint hdc, nint hgdiobj);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool BitBlt(nint hdcDest, int xDest, int yDest, int width, int height,
        nint hdcSrc, int xSrc, int ySrc, uint rop);

    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern int GetDIBits(nint hdc, nint hbm, uint start, uint lines,
        byte[]? bits, ref BitmapInfo info, uint usage);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteDC(nint hdc);

    [LibraryImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool DeleteObject(nint hgdiobj);
}

/// <summary>BITMAPINFO untuk GetDIBits (32 bpp, DIB_RGB_COLORS — bmiColors tidak dipakai).</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct BitmapInfoHeader
{
    public int biSize;
    public int biWidth;
    public int biHeight;
    public short biPlanes;
    public short biBitCount;
    public int biCompression;
    public int biSizeImage;
    public int biXPelsPerMeter;
    public int biYPelsPerMeter;
    public int biClrUsed;
    public int biClrImportant;
}

[StructLayout(LayoutKind.Sequential)]
internal struct BitmapInfo
{
    public BitmapInfoHeader bmiHeader;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
    public uint[] bmiColors;
}
