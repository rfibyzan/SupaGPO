namespace SupaGPO.Core.Visual;

/// <summary>Konfigurasi deteksi visual reel (dipetakan dari FishingSettings).</summary>
public sealed record VisualReelConfig(bool Enabled, int X, int Y, int W, int H, int ThresholdPct)
{
    public bool RegionValid => W > 0 && H > 0;
}

/// <summary>
/// Probe reel visual: capture region layar (dengan cache 100 ms), bandingkan dengan
/// template terkalibrasi. Hasil bool? — true/false = sinyal yakin, null = tidak
/// terkonfigurasi / capture gagal → pemanggil jatuh ke fallback memori. Meniru peran
/// PerfectVisualReelController pada Grand Piece.exe (temuan REA).
/// </summary>
public sealed class VisualReelProbe
{
    private readonly IReelFrameSource _source;
    private readonly Func<VisualReelConfig> _config;
    private readonly Func<long> _clock;
    private const long FrameCacheMs = 100;

    private byte[]? _cachedFrame;
    private long _cachedAt;
    private VisualReelConfig? _cachedCfg;

    public VisualReelDetector? Detector { get; set; }
    public bool HasTemplate => Detector != null;

    /// <summary>Similarity (0..1) dari probe terakhir; null bila belum ada capture valid.</summary>
    public double? LastSimilarity { get; private set; }

    public VisualReelProbe(IReelFrameSource source, Func<VisualReelConfig> config, Func<long>? clock = null)
    {
        _source = source;
        _config = config;
        _clock = clock ?? (() => Environment.TickCount64);
    }

    /// <summary>
    /// Probe reel. true = reel terdeteksi, false = yakin bukan reel, null = tanpa sinyal
    /// (nonaktif / belum kalibrasi / capture gagal).
    /// </summary>
    public bool? Probe()
    {
        var cfg = _config();
        var det = Detector;
        LastSimilarity = null;
        if (!cfg.Enabled || det == null || !cfg.RegionValid)
            return null;
        var frame = CaptureCached(cfg);
        if (frame == null || frame.Length != det.Template.Length)
            return null;
        double sim = VisualReelDetector.Similarity(det.Template, frame);
        LastSimilarity = sim;
        return det.IsReel(sim, cfg.ThresholdPct);
    }

    /// <summary>Capture segar region saat ini (untuk kalibrasi template / uji coba).</summary>
    public byte[]? CaptureRegion()
    {
        var cfg = _config();
        return cfg.RegionValid ? _source.CaptureGray(cfg.X, cfg.Y, cfg.W, cfg.H) : null;
    }

    private byte[]? CaptureCached(VisualReelConfig cfg)
    {
        long now = _clock();
        if (_cachedFrame != null && _cachedCfg == cfg && now - _cachedAt < FrameCacheMs)
            return _cachedFrame;
        var frame = _source.CaptureGray(cfg.X, cfg.Y, cfg.W, cfg.H);
        _cachedFrame = frame;
        _cachedCfg = cfg;
        _cachedAt = now;
        return frame;
    }
}
