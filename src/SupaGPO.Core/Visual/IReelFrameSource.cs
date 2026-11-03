namespace SupaGPO.Core.Visual;

/// <summary>
/// Sumber frame grayscale 8-bit untuk deteksi visual reel. Asli (REA:
/// PerfectVisualReelController / VisualReelMetrics) mendeteksi reel dari piksel layar,
/// bukan dari nama elemen GUI — interface ini adalah seam agar detector tetap pure & testable.
/// </summary>
public interface IReelFrameSource
{
    /// <summary>Capture region layar (koordinat virtual-screen) jadi grayscale w*h byte. null bila gagal — tolerant, tidak throw.</summary>
    byte[]? CaptureGray(int x, int y, int w, int h);
}
