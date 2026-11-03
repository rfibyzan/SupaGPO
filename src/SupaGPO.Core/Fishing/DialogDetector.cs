using SupaGPO.Core.Visual;

namespace SupaGPO.Core.Fishing;

/// <summary>Klaster terang di dalam dialog (tombol/input) + density isi piksel terang.</summary>
public sealed record DialogButton(PixelRect Rect, double Density);

/// <summary>Hasil scan band bawah layar untuk dialog pembelian bait GPO (koordinat layar).</summary>
public sealed record DialogScan(bool Bar, PixelRect BarRect, IReadOnlyList<DialogButton> Buttons, bool RedText)
{
    public static readonly DialogScan None = new(false, default, Array.Empty<DialogButton>(), false);
}

/// <summary>
/// Deteksi dialog beli bait dari frame band bawah client (≈ y 70–95%): bar coklat
/// (panel teks GPO) + klaster piksel terang ber-density tinggi (tombol Yes/Buy/Cancel,
/// input jumlah). Baris yang memuat tombol lolos lewat jumlah-piksel-coklat (bukan run),
/// sehingga tombol di dalam bar tidak memecah bar. Klik hanya boleh di dalam bar.
/// Pure function supaya bisa diuji dengan frame sintetis.
/// </summary>
public static class DialogDetector
{
    private const int MinBarRows = 8;          // bar minimal 8 baris
    private const int BarGapRows = 40;         // jeda antar kelompok baris (jembati baris tombol)
    private const double BarRowBrownMin = 0.15; // ≥15% lebar frame piksel coklat = baris bar
    private const double BarWidthMin = 0.25;   // bar ≥25% lebar frame (menolak tanah selebar layar)
    private const double BarWidthMax = 0.80;   // dan ≤80%
    private const double BarHeightMax = 0.85;  // tinggi bar ≤85% band (menolak tanah tinggi)
    private const int BrightRowMin = 8;        // baris "aktif" bila ≥8 piksel terang
    private const int RowBandGap = 6;          // jeda antar pita baris terang
    private const int ColGroupGap = 12;        // jeda kolom antar klaster dalam satu pita
    private const int ClusterMinWidth = 20;
    private const int ClusterMinHeight = 10;
    private const double ClusterMinDensity = 0.28;

    public static DialogScan Analyze(ColorFrame f, int originX, int originY)
    {
        int w = f.Width, h = f.Height;
        if (w < 40 || h < 10) return DialogScan.None;

        // --- 1) Baris bar: ≥15% piksel coklat (baris ber-tombol tetap lolos).
        var rows = new List<int>();
        for (int y = 0; y < h; y++)
        {
            int brown = 0;
            for (int x = 0; x < w; x++)
            {
                f.Rgb(x, y, out int r, out int g, out int b);
                if (IsBrown(r, g, b)) brown++;
            }
            if (brown >= w * BarRowBrownMin) rows.Add(y);
        }
        if (rows.Count < MinBarRows) return DialogScan.None;

        // --- 2) Kelompokkan baris berurutan (gap ≤40 baris = bar terbesar).
        int gStart = 0, bG = 0, bLen = 0;
        for (int i = 1; i <= rows.Count; i++)
        {
            if (i < rows.Count && rows[i] - rows[i - 1] <= BarGapRows + 1) continue;
            int len = i - gStart;
            if (len > bLen) { bLen = len; bG = gStart; }
            gStart = i;
        }
        int by0 = rows[bG], by1 = rows[bG + bLen - 1];

        // Extent horizontal coklat dalam kelompok itu.
        int bx0 = w, bx1 = -1;
        for (int y = by0; y <= by1; y++)
            for (int x = 0; x < w; x++)
            {
                f.Rgb(x, y, out int r, out int g, out int b);
                if (IsBrown(r, g, b)) { if (x < bx0) bx0 = x; if (x > bx1) bx1 = x; }
            }
        if (bx1 < 0) return DialogScan.None;
        var bar = new PixelRect(originX + bx0, originY + by0, bx1 - bx0 + 1, by1 - by0 + 1);
        if (bar.W < w * BarWidthMin || bar.W > w * BarWidthMax) return DialogScan.None;
        if (bar.H > h * BarHeightMax) return DialogScan.None;

        // --- 3) Klaster terang di dalam bar: pita baris → grup kolom → filter density.
        var bright = new bool[w * h];
        for (int y = by0; y <= by1; y++)
            for (int x = bx0; x <= bx1; x++)
            {
                f.Rgb(x, y, out int r, out int g, out int b);
                if (IsBright(r, g, b)) bright[y * w + x] = true;
            }

        var buttons = new List<DialogButton>();
        var activeRows = new List<int>();
        for (int y = by0; y <= by1; y++)
        {
            int c = 0;
            for (int x = bx0; x <= bx1; x++) if (bright[y * w + x]) c++;
            if (c >= BrightRowMin) activeRows.Add(y);
        }
        int rb = 0;
        for (int i = 1; i <= activeRows.Count; i++)
        {
            if (i < activeRows.Count && activeRows[i] - activeRows[i - 1] <= RowBandGap + 1) continue;
            var band = activeRows.Skip(rb).Take(i - rb).ToList();
            rb = i;
            if (band.Count < ClusterMinHeight) continue;
            buttons.AddRange(ClustersInBand(band, bx0, bx1, w, bright, originX, originY));
        }

        // --- 4) Teks merah "maximum amount" di dalam bar.
        int red = 0;
        for (int y = by0; y <= by1; y++)
            for (int x = bx0; x <= bx1; x++)
            {
                f.Rgb(x, y, out int r, out int g, out int b);
                if (r >= 140 && g <= 85 && b <= 85 && r - g >= 60) red++;
            }

        buttons.Sort((a, b) => a.Rect.X.CompareTo(b.Rect.X));
        return new DialogScan(true, bar, buttons, red >= 30);
    }

    /// <summary>Grup kolom terang dalam satu pita baris → klaster lolos filter ukuran/density.</summary>
    private static List<DialogButton> ClustersInBand(List<int> band, int bx0, int bx1, int w,
        bool[] bright, int originX, int originY)
    {
        var result = new List<DialogButton>();
        int y0 = band[0], y1 = band[^1];
        int cStart = -1, gap = 0;
        for (int x = bx0; x <= bx1 + 1; x++)
        {
            bool any = x <= bx1 && band.Any(y => bright[y * w + x]);
            if (any)
            {
                if (cStart < 0) cStart = x;
                gap = 0;
            }
            else if (cStart >= 0)
            {
                if (++gap > ColGroupGap)
                {
                    AddCluster(result, cStart, x - gap, y0, y1, w, bright, originX, originY);
                    cStart = -1;
                }
            }
        }
        if (cStart >= 0)
            AddCluster(result, cStart, bx1, y0, y1, w, bright, originX, originY);
        return result;
    }

    private static void AddCluster(List<DialogButton> result, int x0, int x1, int y0, int y1, int w,
        bool[] bright, int originX, int originY)
    {
        int cw = x1 - x0 + 1, ch = y1 - y0 + 1;
        if (cw < ClusterMinWidth || ch < ClusterMinHeight) return;
        int count = 0;
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                if (bright[y * w + x]) count++;
        double density = (double)count / (cw * ch);
        if (density >= ClusterMinDensity)
            result.Add(new DialogButton(new PixelRect(originX + x0, originY + y0, cw, ch), density));
    }

    /// <summary>Coklat panel dialog GPO: r&gt;g&gt;b dengan selisih cukup, tidak terlalu gelap/terang.</summary>
    private static bool IsBrown(int r, int g, int b) =>
        r > g + 6 && g > b && r - b >= 12 && r >= 40 && r <= 240 && b <= 150;

    /// <summary>Tombol/input terang (krem/putih): semua kanal tinggi.</summary>
    private static bool IsBright(int r, int g, int b) =>
        r >= 170 && g >= 160 && b >= 125;
}
