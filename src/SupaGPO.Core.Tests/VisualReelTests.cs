using SupaGPO.Core.Fishing;
using SupaGPO.Core.Offsets;
using SupaGPO.Core.Process;
using SupaGPO.Core.Roblox;
using SupaGPO.Core.Visual;

namespace SupaGPO.Core.Tests;

public sealed class FakeFrameSource : IReelFrameSource
{
    public byte[]? Next { get; set; }
    public int Calls { get; private set; }
    public byte[]? CaptureGray(int x, int y, int w, int h) { Calls++; return Next; }
}

public class VisualReelDetectorTests
{
    private static byte[] Fill(int n, byte v) => Enumerable.Repeat(v, n).Select(b => (byte)b).ToArray();

    [Fact]
    public void Similarity_identical_is_one()
    {
        var t = Fill(100, 128);
        Assert.Equal(1.0, VisualReelDetector.Similarity(t, (byte[])t.Clone()), 6);
    }

    [Fact]
    public void Similarity_black_vs_white_is_zero()
    {
        Assert.Equal(0.0, VisualReelDetector.Similarity(Fill(64, 0), Fill(64, 255)), 6);
    }

    [Fact]
    public void Similarity_half_diff_is_half()
    {
        var a = Fill(64, 0);
        var b = Fill(64, 128); // mean|Δ| = 128 → similarity 1 − 128/255
        Assert.Equal(1.0 - 128.0 / 255.0, VisualReelDetector.Similarity(a, b), 6);
    }

    [Fact]
    public void Similarity_length_mismatch_is_zero()
    {
        Assert.Equal(0.0, VisualReelDetector.Similarity(Fill(10, 1), Fill(11, 1)));
    }

    [Fact]
    public void Match_null_frame_is_null()
    {
        var det = new VisualReelDetector(4, 4, Fill(16, 7));
        Assert.Null(det.Match(null));
    }

    [Fact]
    public void Match_wrong_size_is_null()
    {
        var det = new VisualReelDetector(4, 4, Fill(16, 7));
        Assert.Null(det.Match(Fill(8, 7)));
    }

    [Fact]
    public void IsReel_respects_threshold()
    {
        var det = new VisualReelDetector(1, 1, Fill(1, 0));
        Assert.True(det.IsReel(0.70, 70));
        Assert.False(det.IsReel(0.69, 70));
        Assert.True(det.IsReel(1.0, 100)); // boundary inclusive
    }

    [Fact]
    public void Constructor_rejects_bad_template()
    {
        Assert.Throws<ArgumentException>(() => new VisualReelDetector(4, 4, Fill(10, 0)));
    }
}

public class ReelTemplateStoreTests
{
    [Fact]
    public void Save_load_roundtrip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"sgreel-{Guid.NewGuid():N}.bin");
        try
        {
            var pixels = Enumerable.Range(0, 100).Select(i => (byte)(i % 256)).ToArray();
            var det = new VisualReelDetector(10, 10, pixels);
            Assert.True(ReelTemplateStore.Save(path, det));

            var loaded = ReelTemplateStore.TryLoad(path);
            Assert.NotNull(loaded);
            Assert.Equal(10, loaded!.Width);
            Assert.Equal(10, loaded.Height);
            Assert.Equal(pixels, loaded.Template);
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }

    [Fact]
    public void Load_missing_or_corrupt_returns_null()
    {
        Assert.Null(ReelTemplateStore.TryLoad(Path.Combine(Path.GetTempPath(), "nope-sgreel.bin")));

        var path = Path.Combine(Path.GetTempPath(), $"sgreel-{Guid.NewGuid():N}.bin");
        try
        {
            File.WriteAllText(path, "garbage-not-a-template");
            Assert.Null(ReelTemplateStore.TryLoad(path));
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }
}

public class VisualReelProbeTests
{
    private static byte[] Fill(int n, byte v) => Enumerable.Repeat(v, n).Select(b => (byte)b).ToArray();

    private static VisualReelProbe Make(FakeFrameSource src, VisualReelConfig cfg,
        VisualReelDetector? det, Func<long>? clock = null)
    {
        var probe = new VisualReelProbe(src, () => cfg, clock);
        probe.Detector = det;
        return probe;
    }

    [Fact]
    public void Probe_disabled_returns_null()
    {
        var src = new FakeFrameSource { Next = Fill(4, 9) };
        var det = new VisualReelDetector(2, 2, Fill(4, 9));
        var probe = Make(src, new VisualReelConfig(false, 0, 0, 2, 2, 70), det);
        Assert.Null(probe.Probe());
        Assert.Equal(0, src.Calls);
    }

    [Fact]
    public void Probe_no_template_returns_null()
    {
        var src = new FakeFrameSource { Next = Fill(4, 9) };
        var probe = Make(src, new VisualReelConfig(true, 0, 0, 2, 2, 70), null);
        Assert.Null(probe.Probe());
    }

    [Fact]
    public void Probe_invalid_region_returns_null()
    {
        var src = new FakeFrameSource { Next = Fill(4, 9) };
        var det = new VisualReelDetector(2, 2, Fill(4, 9));
        var probe = Make(src, new VisualReelConfig(true, 0, 0, 0, 0, 70), det);
        Assert.Null(probe.Probe());
    }

    [Fact]
    public void Probe_matching_frame_returns_true_with_similarity()
    {
        var src = new FakeFrameSource { Next = Fill(4, 9) };
        var det = new VisualReelDetector(2, 2, Fill(4, 9));
        var probe = Make(src, new VisualReelConfig(true, 5, 5, 2, 2, 70), det);
        Assert.True(probe.Probe());
        Assert.Equal(1.0, probe.LastSimilarity!.Value, 6);
    }

    [Fact]
    public void Probe_different_frame_below_threshold_returns_false()
    {
        var src = new FakeFrameSource { Next = Fill(4, 255) };
        var det = new VisualReelDetector(2, 2, Fill(4, 0));
        var probe = Make(src, new VisualReelConfig(true, 0, 0, 2, 2, 70), det);
        Assert.False(probe.Probe());
    }

    [Fact]
    public void Probe_capture_failure_returns_null()
    {
        var src = new FakeFrameSource { Next = null };
        var det = new VisualReelDetector(2, 2, Fill(4, 9));
        var probe = Make(src, new VisualReelConfig(true, 0, 0, 2, 2, 70), det);
        Assert.Null(probe.Probe());
    }

    [Fact]
    public void Probe_caches_frame_within_100ms()
    {
        long now = 1000;
        var src = new FakeFrameSource { Next = Fill(4, 9) };
        var det = new VisualReelDetector(2, 2, Fill(4, 9));
        var probe = Make(src, new VisualReelConfig(true, 0, 0, 2, 2, 70), det, () => now);
        Assert.True(probe.Probe());
        Assert.True(probe.Probe());
        Assert.Equal(1, src.Calls); // cache hit — tidak capture ulang
        now += 200;
        Assert.True(probe.Probe());
        Assert.Equal(2, src.Calls); // lewat 100 ms → capture baru
    }

    [Fact]
    public void CaptureRegion_uses_current_config()
    {
        var src = new FakeFrameSource { Next = Fill(4, 3) };
        var probe = Make(src, new VisualReelConfig(true, 7, 9, 2, 2, 70), null);
        Assert.Equal(Fill(4, 3), probe.CaptureRegion());
        Assert.Equal(1, src.Calls);
    }
}

public class VisualReelCompositeTests
{
    private static byte[] Fill(int n, byte v) => Enumerable.Repeat(v, n).Select(b => (byte)b).ToArray();

    private static (RobloxFishingSensors sensors, FakeFrameSource src) MakeSensors(byte? frame, byte[] template)
    {
        var src = new FakeFrameSource { Next = frame is null ? null : Fill(template.Length, frame.Value) };
        var probe = new VisualReelProbe(src, () => new VisualReelConfig(true, 0, 0, 4, 4, 70));
        probe.Detector = new VisualReelDetector(4, 4, template);
        var reader = new RobloxMemoryReader(new ProcessAttacher(), OffsetsSnapshot.Load("{}"));
        return (new RobloxFishingSensors(reader, probe), src);
    }

    [Fact]
    public void Visual_match_true_wins_without_roblox()
    {
        var (s, _) = MakeSensors(200, Fill(16, 200));
        Assert.True(s.ReelActive); // tidak ada Roblox, tidak ada offset — visual tetap jalan
    }

    [Fact]
    public void Visual_mismatch_falls_back_to_memory_default_false()
    {
        var (s, _) = MakeSensors(0, Fill(16, 255));
        Assert.False(s.ReelActive); // visual false + Roblox absen → default false, tanpa throw
    }

    [Fact]
    public void No_visual_probe_falls_back_gracefully()
    {
        var reader = new RobloxMemoryReader(new ProcessAttacher(), OffsetsSnapshot.Load("{}"));
        var s = new RobloxFishingSensors(reader);
        Assert.False(s.ReelActive);
        Assert.Equal(0, s.BaitCount);
        Assert.Empty(s.SpawnNotices);
    }
}
