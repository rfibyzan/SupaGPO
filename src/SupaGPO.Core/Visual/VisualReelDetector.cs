namespace SupaGPO.Core.Visual;

/// <summary>
/// Template-matching reel detector (pure, tanpa I/O): similarity = 1 − mean|Δ|/255
/// antara template grayscale terkalibrasi dan frame region saat ini. Threshold dalam
/// persen. Mirip peran VisualReelMetrics pada Grand Piece.exe (observasi REA);
/// tuning konkret (threshold) menunggu runtime observation.
/// </summary>
public sealed class VisualReelDetector
{
    public int Width { get; }
    public int Height { get; }
    public byte[] Template { get; }

    public VisualReelDetector(int width, int height, byte[] template)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (template.Length != width * height) throw new ArgumentException("Template size mismatch.", nameof(template));
        Width = width;
        Height = height;
        Template = template;
    }

    /// <summary>Similarity 0..1; 0 bila ukuran tidak cocok / kosong.</summary>
    public static double Similarity(byte[] template, byte[] frame)
    {
        if (template.Length == 0 || template.Length != frame.Length) return 0;
        long sum = 0;
        for (int i = 0; i < template.Length; i++)
            sum += Math.Abs(template[i] - frame[i]);
        return 1.0 - (double)sum / (template.Length * 255.0);
    }

    /// <summary>Match frame terhadap template; null bila frame null / ukuran salah.</summary>
    public double? Match(byte[]? frame) =>
        frame is null || frame.Length != Template.Length ? null : Similarity(Template, frame);

    /// <summary>Similarity (0..1) dianggap reel bila ≥ thresholdPct.</summary>
    public bool IsReel(double similarity, int thresholdPct) =>
        similarity * 100.0 >= Math.Clamp(thresholdPct, 1, 100);
}
