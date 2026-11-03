using SupaGPO.Core.Visual;

namespace SupaGPO.Core.Diagnostics;

/// <summary>
/// Dump debug reel: frame BMP kecil (ROI bar) + log CSV (posisi kursor, target, hold,
/// progress, durasi capture) saat tuning detektor piksel di game asli. Tahan folder debug
/// kecil: maks ~120 frame per sesi, throttle 100 ms. Tidak pernah throw.
/// </summary>
public sealed class ReelDebugDumper
{
    public const int MaxFrames = 120;
    private readonly string _dir;
    private int _frames;
    private long _lastDump;
    private StreamWriter? _log;

    public ReelDebugDumper(string dir)
    {
        _dir = dir;
        try { Directory.CreateDirectory(_dir); } catch { }
    }

    /// <summary>Baris log CSV: now;pesan. File dibuka sekali per run dalam mode append.</summary>
    public void Log(long now, string msg)
    {
        try
        {
            _log ??= new StreamWriter(Path.Combine(_dir, "reel-log.csv"), append: true);
            _log.WriteLine($"{now};{msg}");
            _log.Flush();
        }
        catch { }
    }

    /// <summary>Simpan frame ROI sebagai BMP bila kuota/throttle mengizinkan.</summary>
    public void DumpFrame(long now, ColorFrame f, PixelRect origin)
    {
        if (_frames >= MaxFrames || now - _lastDump < 100) return;
        _lastDump = now;
        try
        {
            SaveBmp(Path.Combine(_dir, $"frame-{_frames++:D3}.bmp"), f, origin);
        }
        catch { }
    }

    /// <summary>Reset kuota frame untuk run baru; log di-append dengan header sesi (data lama tetap ada).</summary>
    public void ResetRun()
    {
        _frames = 0;
        try { _log?.Dispose(); _log = null; } catch { }
        Log(Environment.TickCount64, $"=== SESSION {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
    }

    private static void SaveBmp(string path, ColorFrame f, PixelRect origin)
    {
        int w = f.Width, h = f.Height;
        int rowBytes = w * 3, pad = (4 - rowBytes % 4) % 4, stride = rowBytes + pad;
        using var fs = File.Create(path);
        using var bw = new BinaryWriter(fs);
        bw.Write((byte)'B'); bw.Write((byte)'M');
        bw.Write(54 + stride * h); bw.Write(0); bw.Write(54);
        bw.Write(40); bw.Write(w); bw.Write(-h); // negatif = top-down
        bw.Write((short)1); bw.Write((short)24); bw.Write(0);
        bw.Write(stride * h); bw.Write(2835); bw.Write(2835); bw.Write(0); bw.Write(0);
        var row = new byte[stride];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = (y * w + x) * 4;
                row[x * 3] = f.Bgra[i]; row[x * 3 + 1] = f.Bgra[i + 1]; row[x * 3 + 2] = f.Bgra[i + 2];
            }
            bw.Write(row);
        }
    }
}
