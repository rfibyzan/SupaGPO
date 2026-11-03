using SupaGPO.Core.Fishing;
using SupaGPO.Core.Input;
using SupaGPO.Core.Settings;
using SupaGPO.Core.Visual;

namespace SupaGPO.Core.Tests;

public class DialogAndRefillTests
{
    // ===== HotkeyMap =====

    [Theory]
    [InlineData("F1")]
    [InlineData("F12")]
    [InlineData("0")]
    [InlineData("9")]
    [InlineData("A")]
    [InlineData("Z")]
    [InlineData("SPACE")]
    [InlineData("ENTER")]
    [InlineData("MOUSE4")]
    [InlineData("MOUSE5")]
    public void HotkeyMap_RoundTrips(string name)
    {
        ushort vk = HotkeyMap.ToVirtualKey(name);
        Assert.NotEqual((ushort)0, vk);
        Assert.Equal(name, HotkeyMap.Name(vk));
    }

    [Fact]
    public void HotkeyMap_UnknownName_DefaultsF3()
    {
        Assert.Equal(0x72, HotkeyMap.ToVirtualKey("??"));
        Assert.Equal(0x72, HotkeyMap.ToVirtualKey(""));
    }

    [Fact]
    public void HotkeyMap_Capturable_ContainsNoDuplicates()
    {
        var keys = HotkeyMap.CapturableKeys;
        Assert.Equal(keys.Length, keys.Distinct().Count());
        Assert.Contains((ushort)0x72, keys); // F3 default
    }

    // ===== DialogDetector =====

    /// <summary>Frame band bawah: bar coklat + klaster terang (tombol/input) opsional teks merah.</summary>
    private static ColorFrame DialogFrame(int w, int h, (int X, int W)[] buttons, bool redText = false)
    {
        var f = new ColorFrame(w, h, new byte[w * h * 4]);
        void Set(int x, int y, int r, int g, int b)
        {
            int i = (y * w + x) * 4;
            f.Bgra[i] = (byte)b; f.Bgra[i + 1] = (byte)g; f.Bgra[i + 2] = (byte)r; f.Bgra[i + 3] = 255;
        }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                Set(x, y, 25, 30, 60); // latar biru gelap (bukan coklat)
        // Bar coklat: x 15–85%, y 20–70%.
        for (int y = h * 20 / 100; y < h * 70 / 100; y++)
            for (int x = w * 15 / 100; x < w * 85 / 100; x++)
                Set(x, y, 120, 85, 55);
        // Tombol terang di dalam bar.
        foreach (var (bx, bw) in buttons)
            for (int y = h * 30 / 100; y < h * 42 / 100; y++)
                for (int x = bx; x < bx + bw && x < w; x++)
                    Set(x, y, 235, 230, 210);
        if (redText)
            for (int y = h * 50 / 100; y < h * 58 / 100; y++)
                for (int x = w * 20 / 100; x < w * 60 / 100; x++)
                    Set(x, y, 210, 40, 40);
        return f;
    }

    [Fact]
    public void DialogDetector_FindsBarAndButtons()
    {
        var f = DialogFrame(400, 120, new[] { (100, 40), (180, 40) });
        var scan = DialogDetector.Analyze(f, originX: 10, originY: 20);
        Assert.True(scan.Bar);
        Assert.Equal(2, scan.Buttons.Count);
        Assert.False(scan.RedText);
        // Bar di dalam frame, tombol di dalam bar.
        Assert.True(scan.BarRect.X >= 10 && scan.BarRect.Right <= 410);
        Assert.All(scan.Buttons, b =>
        {
            Assert.True(b.Rect.X >= scan.BarRect.X && b.Rect.Right <= scan.BarRect.Right);
            Assert.True(b.Rect.Y >= scan.BarRect.Y && b.Rect.Bottom <= scan.BarRect.Bottom);
        });
        // Urut kiri → kanan.
        Assert.True(scan.Buttons[0].Rect.X < scan.Buttons[1].Rect.X);
    }

    [Fact]
    public void DialogDetector_NoBarOnPlainFrame()
    {
        var f = new ColorFrame(400, 120, new byte[400 * 120 * 4]); // hitam semua
        var scan = DialogDetector.Analyze(f, 0, 0);
        Assert.False(scan.Bar);
        Assert.Empty(scan.Buttons);
    }

    [Fact]
    public void DialogDetector_DetectsRedText()
    {
        var f = DialogFrame(400, 120, new[] { (300, 30) }, redText: true);
        var scan = DialogDetector.Analyze(f, 0, 0);
        Assert.True(scan.Bar);
        Assert.True(scan.RedText);
    }

    // ===== BaitRefillController =====

    private sealed class RefillInput : IInput
    {
        public readonly List<string> Calls = new();
        public readonly HashSet<ushort> HeldKeys = new();
        public bool LeftHeld { get; private set; }
        public long LeftDownSince => 0;
        public bool IsProcessForeground(uint pid) => true;
        public void LeftDown() => Calls.Add("Down");
        public void LeftUp() => Calls.Add("Up");
        public void RightDown() { }
        public void RightUp() { }
        public void ReleaseAllButtons() { ReleaseAllKeys(); }
        public void TapKey(ushort vk, bool extended = false) => Calls.Add($"Key{vk}");
        public bool ClickAtClientPoint(nint hwnd, int x, int y) { Calls.Add($"Click({x},{y})"); return true; }

        public void KeyDown(ushort virtualKey, bool extended = false) => HeldKeys.Add(virtualKey);
        public void KeyUp(ushort virtualKey, bool extended = false) => HeldKeys.Remove(virtualKey);
        public void ReleaseAllKeys() => HeldKeys.Clear();
        public bool IsKeyHeld(ushort virtualKey) => HeldKeys.Contains(virtualKey);
    }

    private sealed class RefillScreen : IGameScreen
    {
        public Func<ColorFrame?> Next = () => null;
        public nint Handle => 0x1;
        public PixelRect? GetClientRect(int pid) => new(0, 0, 800, 600);
        public ColorFrame? Capture(PixelRect r) => Next();
    }

    /// <summary>Dialog Yes: bar coklat + 2 tombol terang.</summary>
    private static ColorFrame YesDialog() => DialogFrame(800, 185, new[] { (280, 50), (360, 50) });

    /// <summary>Dialog jumlah: input + Buy + Cancel.</summary>
    private static ColorFrame QtyDialog() => DialogFrame(800, 185, new[] { (180, 60), (300, 50), (380, 60) });

    [Fact]
    public void Refill_NoDialog_SkipsWithoutClick()
    {
        var input = new RefillInput();
        var screen = new RefillScreen { Next = () => null };
        var settings = new FishingSettings(AutoRefillBait: true, RefillAmount: 10, AutoWalkToBait: false);
        var c = new BaitRefillController(input, screen, () => settings);
        c.Start(0);
        for (long now = 0; now <= 4000; now += 100) c.Step(now, new PixelRect(0, 0, 800, 600));
        Assert.True(c.Finished);
        Assert.True(c.Failed);
        Assert.DoesNotContain(input.Calls, s => s.StartsWith("Click")); // tidak klik tanpa dialog
        Assert.Contains("Key69", input.Calls); // E = 0x45 = 69
    }

    [Fact]
    public void Refill_FullFlow_ClicksYesTypesBuysCloses()
    {
        var input = new RefillInput();
        // Alur: Yes → jumlah → Buy → bar "..." tersisa → tutup.
        var frames = new Queue<ColorFrame?>(new ColorFrame?[] { YesDialog(), YesDialog(), QtyDialog(), QtyDialog(), QtyDialog(), DialogFrame(800, 185, Array.Empty<(int, int)>()) });
        var screen = new RefillScreen { Next = () => frames.Count > 1 ? frames.Dequeue() : frames.Peek() };
        var settings = new FishingSettings(AutoRefillBait: true, RefillAmount: 7, AutoWalkToBait: false);
        var c = new BaitRefillController(input, screen, () => settings);
        c.Start(0);
        long now = 0;
        while (!c.Finished && now < 20000) { c.Step(now, new PixelRect(0, 0, 800, 600)); now += 100; }
        Assert.True(c.Finished);
        Assert.False(c.Failed);
        Assert.Contains("Key55", input.Calls); // '7' = 0x37 = 55
        Assert.True(input.Calls.Count(s => s.StartsWith("Click")) >= 4); // Yes, input, Buy, "..."
    }

    [Fact]
    public void Refill_RedText_MarksFullAndBacksOff()
    {
        var input = new RefillInput();
        var screen = new RefillScreen { Next = () => DialogFrame(800, 185, new[] { (600, 30) }, redText: true) };
        var settings = new FishingSettings(AutoRefillBait: true, RefillAmount: 5, AutoWalkToBait: false);
        var c = new BaitRefillController(input, screen, () => settings);
        c.Start(0);
        long now = 0;
        while (!c.Finished && now < 10000) { c.Step(now, new PixelRect(0, 0, 800, 600)); now += 100; }
        Assert.True(c.Finished);
        Assert.True(c.FullBait);
    }
}
