using SupaGPO.Core.Visual;

namespace SupaGPO.Core.Tests;

/// <summary>
/// Frame sintetis meniru screenshot minigame GPO: bar kiri trek gelap berisi zona biru
/// + garis putih, bar kanan trek gelap berisi isi hijau (progress).
/// </summary>
public class ReelBarAnalyzerTests
{
    private static ColorFrame Blank(int w = 800, int h = 600)
    {
        var f = new ColorFrame(w, h, new byte[w * h * 4]);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                Set(f, x, y, 40, 90, 190); // laut: biru tapi g<100 → bukan zona biru bar, bukan trek gelap
        return f;
    }

    private static void Set(ColorFrame f, int x, int y, int r, int g, int b)
    {
        int i = (y * f.Width + x) * 4;
        f.Bgra[i] = (byte)b; f.Bgra[i + 1] = (byte)g; f.Bgra[i + 2] = (byte)r; f.Bgra[i + 3] = 255;
    }

    private static ColorFrame MinigameFrame(int cursorRow, int blueTop, int blueBottom,
        int greenTop, int greenBottom, int leftX = 300, int rightX = 340,
        int barTop = 100, int barLen = 400)
    {
        var f = Blank();
        for (int y = barTop; y < barTop + barLen; y++)
        {
            for (int x = leftX; x < leftX + 6; x++)
                Set(f, x, y, 30, 30, 35); // trek gelap bar kiri
            for (int x = rightX; x < rightX + 6; x++)
                Set(f, x, y, 30, 30, 35); // trek gelap bar kanan
            bool blue = y >= blueTop && y <= blueBottom;
            for (int x = leftX + 1; x < leftX + 5; x++)
                Set(f, x, y, blue ? 60 : 22, blue ? 160 : 24, blue ? 240 : 26);
            bool green = y >= greenTop && y <= greenBottom;
            for (int x = rightX + 1; x < rightX + 5; x++)
                Set(f, x, y, green ? 120 : 22, green ? 220 : 24, green ? 60 : 26);
        }
        // Garis putih (kursor) 8 px di bar kiri.
        for (int y = cursorRow; y < cursorRow + 8 && y < barTop + barLen; y++)
            for (int x = leftX; x < leftX + 6; x++)
                Set(f, x, y, 235, 235, 235);
        return f;
    }

    [Fact]
    public void MinigameFrame_Detected_WithCursorAndTarget()
    {
        // Zona biru 100-260, kursor di 300 (di luar biru), hijau 150-250.
        var f = MinigameFrame(cursorRow: 300, blueTop: 100, blueBottom: 260, greenTop: 150, greenBottom: 250);
        var r = ReelBarAnalyzer.Analyze(f, clientHeight: 600);
        Assert.NotNull(r);
        Assert.True(Math.Abs(r!.LeftX - 302) <= 2, $"LeftX={r.LeftX}");
        Assert.True(Math.Abs(r.RightX - 342) <= 2, $"RightX={r.RightX}");
        Assert.InRange(r.CursorY ?? -1, 300, 308);
        Assert.False(r.CursorInBlue);
        Assert.NotNull(r.TargetY);
        Assert.True(r.TargetY < 100 || r.TargetY > 260, $"TargetY={r.TargetY} harus di luar zona biru");
        Assert.InRange(r.Progress, 0.2, 0.3); // ~100/380 baris hijau
    }

    [Fact]
    public void CursorInsideBlue_Zone_Reported()
    {
        var f = MinigameFrame(cursorRow: 150, blueTop: 100, blueBottom: 260, greenTop: 150, greenBottom: 250);
        var r = ReelBarAnalyzer.Analyze(f, clientHeight: 600);
        Assert.NotNull(r);
        Assert.True(r!.CursorInBlue, "kursor 150 berada di dalam zona biru 100-260");
    }

    [Fact]
    public void BlankFrame_NoDetection()
    {
        Assert.Null(ReelBarAnalyzer.Analyze(Blank(), clientHeight: 600));
    }

    [Fact]
    public void SingleBarWithoutPartner_NoDetection()
    {
        var f = Blank();
        for (int y = 100; y < 500; y++)
            for (int x = 300; x < 306; x++)
            {
                bool blue = y >= 100 && y <= 260;
                Set(f, x, y, blue ? 60 : 30, blue ? 160 : 30, blue ? 240 : 35);
            }
        Assert.Null(ReelBarAnalyzer.Analyze(f, clientHeight: 600));
    }

    [Fact]
    public void OceanBackground_NoFalsePositive()
    {
        // Air biru lebar + dasar gelap: tidak boleh terbaca sebagai pasangan bar.
        var f = Blank();
        for (int y = 300; y < 600; y++)
            for (int x = 0; x < 800; x++)
                Set(f, x, y, 40, 90, 190);
        Assert.Null(ReelBarAnalyzer.Analyze(f, clientHeight: 600));
    }
}
