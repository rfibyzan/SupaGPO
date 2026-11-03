namespace SupaGPO.Core.Visual;

/// <summary>
/// Hasil baca minigame reel GPO (koordinat frame). Bar kiri: zona biru = bahaya,
/// garis putih = kursor yang digerakkan dengan tahan klik. Bar kanan: isi hijau = progress.
/// </summary>
public sealed record ReelReading(
    int LeftX, int Top, int Bottom,
    int RightX, int RightTop, int RightBottom,
    int? CursorY, int? TargetY, bool CursorInBlue, double Progress);

/// <summary>
/// Deteksi minigame reel dari piksel (meniru PerfectVisualReelController asli, tanpa kalibrasi):
/// cari pasangan bar vertikal bertrek gelap — kiri berisi biru, kanan (offset 2–15% tinggi
/// client) berisi hijau — lalu klasifikasi per baris di bar kiri. Pure, tanpa I/O.
/// </summary>
public static class ReelBarAnalyzer
{
    private const int MaxGap = 4;   // piksel anti-alias yang boleh memutus run
    private const int Window = 3;   // setengah lebar jendela klasifikasi per baris

    public static bool IsDark(int r, int g, int b)
    {
        int mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b));
        return mx <= 60 && mx - mn <= 25;
    }

    public static bool IsBlue(int r, int g, int b) => b >= 170 && b - r >= 80 && g >= 100;
    public static bool IsWhite(int r, int g, int b) => r >= 190 && g >= 190 && b >= 190;
    public static bool IsLime(int r, int g, int b) => g >= 160 && g - r >= 30 && g - b >= 60;

    private readonly record struct Run(int Start, int Len, int Dark, int Primary, int MaxPrimaryRun);

    /// <summary>Run vertikal terpanjang dari piksel {gelap, primer, putih}; primer = biru (kiri) / hijau (kanan).</summary>
    private static Run LongestRun(ColorFrame f, int x, bool left)
    {
        Run best = default;
        int start = -1, last = -1, dark = 0, prim = 0, primRun = 0, maxPrimRun = 0;
        for (int y = 0; y < f.Height; y++)
        {
            f.Rgb(x, y, out int r, out int g, out int b);
            bool isDark = IsDark(r, g, b);
            bool isPrim = left ? IsBlue(r, g, b) : IsLime(r, g, b);
            if (!isDark && !isPrim && !IsWhite(r, g, b))
            {
                primRun = 0;
                continue;
            }
            if (start >= 0 && y - last - 1 > MaxGap)
            {
                if (last - start + 1 > best.Len) best = new Run(start, last - start + 1, dark, prim, maxPrimRun);
                start = -1;
            }
            if (start < 0) { start = y; dark = 0; prim = 0; primRun = 0; maxPrimRun = 0; }
            last = y;
            if (isDark) dark++;
            if (isPrim) { prim++; primRun++; maxPrimRun = Math.Max(maxPrimRun, primRun); }
            else primRun = 0;
        }
        if (start >= 0 && last - start + 1 > best.Len) best = new Run(start, last - start + 1, dark, prim, maxPrimRun);
        return best;
    }

    /// <param name="clientHeight">Tinggi area client Roblox (skala ambang; frame bisa berupa ROI).</param>
    public static ReelReading? Analyze(ColorFrame f, int clientHeight)
    {
        if (f.Width < 8 || f.Height < 8) return null;
        int s = Math.Max(clientHeight, 1);
        int minLeft = Math.Max(40, s * 12 / 100);
        int minOff = Math.Max(8, s * 2 / 100);
        int maxOff = Math.Max(minOff + 8, s * 15 / 100);
        int maxBand = Math.Max(12, s * 4 / 100); // bar sempit; air/latar lebar ditolak

        var lefts = new SortedDictionary<int, Run>();
        for (int x = 0; x < f.Width; x += 2)
        {
            var r = LongestRun(f, x, left: true);
            if (r.Len >= minLeft &&
                r.Primary >= Math.Max(3, r.Len * 3 / 100) &&
                r.MaxPrimaryRun >= Math.Max(6, r.Len * 3 / 100) &&
                r.Dark >= Math.Max(6, r.Len * 3 / 100))
                lefts[x] = r;
        }
        if (lefts.Count == 0) return null;

        var rightCache = new Run?[f.Width];
        Run RightAt(int x)
        {
            var c = rightCache[x];
            if (c is null) { c = LongestRun(f, x, left: false); rightCache[x] = c; }
            return c.Value;
        }

        // Kolom berurutan (langkah 2) → pita; satu kandidat per pita.
        var bands = new List<(int Lo, int Hi)>();
        foreach (int x in lefts.Keys)
        {
            if (bands.Count > 0 && x - bands[^1].Hi <= 2) bands[^1] = (bands[^1].Lo, x);
            else bands.Add((x, x));
        }

        ReelReading? best = null;
        int bestScore = -1;
        foreach (var (lo, hi) in bands)
        {
            if (hi - lo > maxBand) continue;
            int lx = lo + (hi - lo) / 4 * 2; // kolom genap di tengah pita
            var left = lefts[lx];

            bool IsRight(Run rr) =>
                rr.Primary >= 2 &&
                rr.Len * 10 >= left.Len * 4 && rr.Len * 10 <= left.Len * 12 &&
                rr.Dark * 10 >= rr.Len * 3 &&
                rr.Start + rr.Len / 2 >= left.Start && rr.Start + rr.Len / 2 <= left.Start + left.Len;

            int bestRx = -1;
            Run bestR = default;
            int fromX = hi + minOff, toX = Math.Min(f.Width - 1, lx + maxOff);
            for (int rx = fromX; rx <= toX; rx += 2)
            {
                var rr = RightAt(rx);
                if (!IsRight(rr)) continue;
                if (bestRx < 0 || rr.Primary > bestR.Primary || (rr.Primary == bestR.Primary && rr.Len > bestR.Len))
                {
                    bestRx = rx;
                    bestR = rr;
                }
            }
            if (bestRx < 0) continue;

            int rlo = bestRx, rhi = bestRx;
            while (rlo - 2 >= fromX && IsRight(RightAt(rlo - 2))) rlo -= 2;
            while (rhi + 2 <= toX && IsRight(RightAt(rhi + 2))) rhi += 2;
            int rxC = (rlo + rhi) / 2;
            var right = RightAt(rxC);
            if (!IsRight(right)) { rxC = bestRx; right = bestR; }

            int score = left.Primary + right.Primary;
            if (score > bestScore)
            {
                bestScore = score;
                best = Read(f, lx, left, rxC, right);
            }
        }
        return best;
    }

    private static ReelReading Read(ColorFrame f, int lx, Run left, int rx, Run right)
    {
        int top = left.Start, n = left.Len, bottom = top + n - 1;
        int x0 = Math.Max(0, lx - Window), x1 = Math.Min(f.Width - 1, lx + Window);

        // 0 = aman, 1 = biru, 2 = putih (kursor)
        var cls = new byte[n];
        for (int i = 0; i < n; i++)
        {
            int blue = 0, white = 0;
            for (int x = x0; x <= x1; x++)
            {
                f.Rgb(x, top + i, out int r, out int g, out int b);
                if (IsWhite(r, g, b)) white++;
                else if (IsBlue(r, g, b)) blue++;
            }
            cls[i] = white >= 2 ? (byte)2 : blue >= 3 ? (byte)1 : (byte)0;
        }

        // Kursor = run putih terpanjang; baris putih diisi ulang dari tetangganya.
        int? cursor = null;
        bool cursorInBlue = false;
        int bestWhite = 0;
        for (int i = 0; i < n;)
        {
            if (cls[i] != 2) { i++; continue; }
            int j = i;
            while (j + 1 < n && cls[j + 1] == 2) j++;
            bool above = i > 0 && cls[i - 1] == 1;
            bool below = j < n - 1 && cls[j + 1] == 1;
            byte fill = above && below ? (byte)1 : (byte)0;
            if (j - i + 1 > bestWhite)
            {
                bestWhite = j - i + 1;
                cursor = top + (i + j) / 2;
                cursorInBlue = fill == 1;
            }
            for (int k = i; k <= j; k++) cls[k] = fill;
            i = j + 1;
        }

        // Segmen aman (bukan biru) dengan panjang minimum.
        int minSafe = Math.Max(3, n * 4 / 100);
        var segs = new List<(int S, int E)>();
        for (int i = 0; i < n;)
        {
            if (cls[i] != 0) { i++; continue; }
            int j = i;
            while (j + 1 < n && cls[j + 1] == 0) j++;
            if (j - i + 1 >= minSafe) segs.Add((i, j));
            i = j + 1;
        }

        int? target = null;
        if (segs.Count > 0)
        {
            (int S, int E) pick = segs[0];
            if (cursor is int c)
            {
                int ci = c - top, bestDist = int.MaxValue;
                foreach (var sg in segs)
                {
                    int d = ci < sg.S ? sg.S - ci : ci > sg.E ? ci - sg.E : 0;
                    if (d < bestDist || (d == bestDist && sg.E - sg.S > pick.E - pick.S))
                    {
                        bestDist = d;
                        pick = sg;
                    }
                }
            }
            else
            {
                foreach (var sg in segs)
                    if (sg.E - sg.S > pick.E - pick.S) pick = sg;
            }
            target = top + (pick.S + pick.E) / 2;
        }

        // Progress = proporsi baris hijau di bar kanan.
        int rx0 = Math.Max(0, rx - Window), rx1 = Math.Min(f.Width - 1, rx + Window), lime = 0;
        for (int y = right.Start; y < right.Start + right.Len; y++)
        {
            int cnt = 0;
            for (int x = rx0; x <= rx1; x++)
            {
                f.Rgb(x, y, out int r, out int g, out int b);
                if (IsLime(r, g, b)) cnt++;
            }
            if (cnt >= 3) lime++;
        }
        double progress = right.Len > 0 ? (double)lime / right.Len : 0;

        return new ReelReading(lx, top, bottom, rx, right.Start, right.Start + right.Len - 1,
            cursor, target, cursorInBlue, progress);
    }
}
