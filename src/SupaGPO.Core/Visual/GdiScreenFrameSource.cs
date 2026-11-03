using SupaGPO.Core.Interop;

namespace SupaGPO.Core.Visual;

/// <summary>
/// Capture region layar via GDI (GetDC + BitBlt + GetDIBits) → grayscale 8-bit.
/// Tolerant: region di luar batas / API gagal → null, tidak throw.
/// </summary>
public sealed class GdiScreenFrameSource : IReelFrameSource
{
    public const int MaxDimension = 1024;

    public byte[]? CaptureGray(int x, int y, int w, int h)
    {
        if (w <= 0 || h <= 0 || w > MaxDimension || h > MaxDimension) return null;
        nint screenDc = 0, memDc = 0, bmp = 0, oldObj = 0;
        try
        {
            screenDc = NativeMethods.GetDC(0);
            if (screenDc == 0) return null;
            memDc = NativeMethods.CreateCompatibleDC(screenDc);
            if (memDc == 0) return null;
            bmp = NativeMethods.CreateCompatibleBitmap(screenDc, w, h);
            if (bmp == 0) return null;
            oldObj = NativeMethods.SelectObject(memDc, bmp);
            if (oldObj == 0) return null;
            if (!NativeMethods.BitBlt(memDc, 0, 0, w, h, screenDc, x, y, NativeMethods.Srccopy))
                return null;

            var pixels = new byte[w * h * 4];
            var info = new BitmapInfo
            {
                bmiHeader = new BitmapInfoHeader
                {
                    biSize = 40,
                    biWidth = w,
                    biHeight = h, // positif = bottom-up; dibalik saat konversi
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = 0,
                    biSizeImage = pixels.Length,
                },
                bmiColors = new uint[256],
            };
            if (NativeMethods.GetDIBits(memDc, bmp, 0, (uint)h, pixels, ref info, NativeMethods.DiRgbColors) == 0)
                return null;

            var gray = new byte[w * h];
            for (int row = 0; row < h; row++)
            {
                int srcRow = (h - 1 - row) * w * 4; // flip bottom-up
                int dst = row * w;
                for (int col = 0; col < w; col++)
                {
                    int p = srcRow + col * 4;
                    // BGRA → luminance (BT.601 integer)
                    gray[dst + col] = (byte)((pixels[p] * 29 + pixels[p + 1] * 150 + pixels[p + 2] * 77) >> 8);
                }
            }
            return gray;
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
}
