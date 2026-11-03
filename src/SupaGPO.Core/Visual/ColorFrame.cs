namespace SupaGPO.Core.Visual;

/// <summary>Persegi piksel di koordinat layar (virtual screen).</summary>
public readonly record struct PixelRect(int X, int Y, int W, int H)
{
    public int Right => X + W;
    public int Bottom => Y + H;
}

/// <summary>Frame warna BGRA 32-bit, baris top-down.</summary>
public sealed class ColorFrame
{
    public ColorFrame(int width, int height, byte[] bgra)
    {
        if (width <= 0 || height <= 0 || bgra.Length < width * height * 4)
            throw new ArgumentException("Invalid frame size.");
        Width = width;
        Height = height;
        Bgra = bgra;
    }

    public int Width { get; }
    public int Height { get; }
    public byte[] Bgra { get; }

    public void Rgb(int x, int y, out int r, out int g, out int b)
    {
        int i = (y * Width + x) * 4;
        b = Bgra[i];
        g = Bgra[i + 1];
        r = Bgra[i + 2];
    }
}

/// <summary>Seam layar game: area client jendela Roblox + capture warna. Tolerant: null bila gagal.</summary>
public interface IGameScreen
{
    /// <summary>Handle jendela Roblox (0 bila belum diketahui) — untuk klik koordinat client.</summary>
    nint Handle { get; }
    PixelRect? GetClientRect(int processId);
    ColorFrame? Capture(PixelRect screenRect);
}
