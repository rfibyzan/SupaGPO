using SupaGPO.Core.Fishing;
using SupaGPO.Core.Input;
using SupaGPO.Core.Settings;
using SupaGPO.Core.Visual;

namespace SupaGPO.Core.Tests;

public class GpoFishingMacroTests
{
    private sealed class FakeInput : IInput
    {
        public int DownCount, UpCount, TapCount;
        public readonly HashSet<ushort> HeldKeys = new();
        public bool Foreground = true;
        public bool IsProcessForeground(uint processId) => Foreground;
        public void LeftDown() => DownCount++;
        public void LeftUp() => UpCount++;
        public void RightDown() { }
        public void RightUp() { }
        public void ReleaseAllButtons() => LeftUp();
        public void TapKey(ushort virtualKey, bool extended = false) => TapCount++;
        public bool ClickAtClientPoint(nint hwnd, int clientX, int clientY) => true;
        public bool LeftHeld => DownCount > UpCount;
        public long LeftDownSince => 0;

        public void KeyDown(ushort virtualKey, bool extended = false) => HeldKeys.Add(virtualKey);
        public void KeyUp(ushort virtualKey, bool extended = false) => HeldKeys.Remove(virtualKey);
        public void ReleaseAllKeys() => HeldKeys.Clear();
        public bool IsKeyHeld(ushort virtualKey) => HeldKeys.Contains(virtualKey);
    }

    private sealed class FakeScreen : IGameScreen
    {
        public int Captures;
        public Func<int, ColorFrame?> OnCapture = _ => null;
        public nint Handle => 0x1234;
        public PixelRect? GetClientRect(int processId) => new(0, 0, 800, 600);
        public ColorFrame? Capture(PixelRect screenRect) => OnCapture(++Captures);
    }

    private static ColorFrame Frame(bool bars, int cursorRow = 300)
    {
        int w = 800, h = 600;
        var f = new ColorFrame(w, h, new byte[w * h * 4]);
        void Set(int x, int y, int r, int g, int b)
        {
            int i = (y * w + x) * 4;
            f.Bgra[i] = (byte)b; f.Bgra[i + 1] = (byte)g; f.Bgra[i + 2] = (byte)r; f.Bgra[i + 3] = 255;
        }
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                Set(x, y, 40, 90, 190);
        if (!bars) return f;
        for (int y = 100; y < 500; y++)
        {
            for (int x = 300; x < 306; x++) Set(x, y, 30, 30, 35);
            for (int x = 340; x < 346; x++) Set(x, y, 30, 30, 35);
            bool blue = y >= 100 && y <= 260;
            for (int x = 301; x < 305; x++) Set(x, y, blue ? 60 : 22, blue ? 160 : 24, blue ? 240 : 26);
            bool green = y >= 150 && y <= 250;
            for (int x = 341; x < 345; x++) Set(x, y, green ? 120 : 22, green ? 220 : 24, green ? 60 : 26);
        }
        for (int y = cursorRow; y < cursorRow + 8 && y < 500; y++)
            for (int x = 300; x < 306; x++) Set(x, y, 235, 235, 235);
        return f;
    }

    private static (GpoFishingMacro Macro, FakeInput Input, FakeScreen Screen) Make(
        Func<int, ColorFrame?>? frames = null, bool equipOnStart = false,
        FishingSettings? settings = null)
    {
        var input = new FakeInput();
        var screen = new FakeScreen();
        if (frames is not null) screen.OnCapture = frames;
        settings ??= new FishingSettings(CastClickMs: 50, BiteTimeoutSec: 60, AfterCatchMs: 700,
            EquipOnStart: equipOnStart);
        long now = 0;
        var macro = new GpoFishingMacro(input, screen, () => settings, () => 1234, () => now, autoThread: false);
        return (macro, input, screen);
    }

    [Fact]
    public void EquipOnStart_TapsRodSlot_BeforeFirstCast()
    {
        var (macro, input, _) = Make(equipOnStart: true);
        macro.Start();
        macro.Step(0);                          // Idle → tap slot rod → CastDown (tunda)
        Assert.Equal(1, input.TapCount);
        Assert.Equal(FishingPhase.CastDown, macro.Phase);
        Assert.Equal(0, input.DownCount);       // belum cast
        macro.Step(400);                        // jeda equip → cast
        Assert.Equal(1, input.DownCount);
    }

    [Fact]
    public void Cast_IsShortClick_ThenWaitsForBite()
    {
        var (macro, input, _) = Make();
        macro.Start();
        Assert.Equal(FishingPhase.Idle, macro.Phase);
        macro.Step(1000);                       // Idle → CastDown → CastUp (LeftDown)
        Assert.Equal(FishingPhase.CastUp, macro.Phase);
        Assert.Equal(1, input.DownCount);
        macro.Step(1050);                       // CastUp → LeftUp, WaitBite
        Assert.Equal(FishingPhase.WaitBite, macro.Phase);
        Assert.Equal(1, input.UpCount);
    }

    [Fact]
    public void NoBite_Timeout_Recasts()
    {
        var (macro, input, _) = Make();
        macro.Start();
        macro.Step(1000);
        macro.Step(1050);                       // biteSince=1050
        macro.Step(1050 + 250);                 // settle lewat, frame kosong → waiting
        Assert.Equal(FishingPhase.WaitBite, macro.Phase);
        macro.Step(1050 + 60_000 + 1);          // timeout → recast (biteTimeouts=1, ganjil → CastDown)
        Assert.Equal(FishingPhase.CastDown, macro.Phase);
        macro.Step(1050 + 60_000 + 60);         // cast ulang
        Assert.Equal(2, input.DownCount);
    }

    [Fact]
    public void BiteDetected_StartsReeling_AndCompletesMinigame()
    {
        // Frame bar ada untuk 60 capture pertama (≈900 ms reel), lalu hilang.
        var (macro, input, _) = Make(n => n <= 60 ? Frame(bars: true, cursorRow: 120) : Frame(bars: false));
        macro.Start();
        macro.Step(1000);
        macro.Step(1050);                       // WaitBite mulai
        macro.Step(1050 + 250);                 // bite terdeteksi → Reel (cap 1)
        Assert.Equal(FishingPhase.Reel, macro.Phase);
        long last = 1050 + 250;
        for (int i = 0; i < 60; i++)            // reel aktif ±900 ms (cap 2..61)
        {
            last += 15;
            macro.Step(last);
        }
        Assert.True(input.DownCount >= 2, $"reel harus menahan klik: Down={input.DownCount} Up={input.UpCount} Phase={macro.Phase} Status={macro.StatusText}");
        // Bar hilang >600 ms, reelMs ≥800 → minigame sah dihitung → Cooldown.
        macro.Step(last + 601);
        Assert.Equal(FishingPhase.Cooldown, macro.Phase);
        Assert.Equal(1, macro.Minigames);
        macro.Step(last + 601 + 701);           // cooldown habis → cast ulang
        Assert.Equal(FishingPhase.CastDown, macro.Phase);
    }

    [Fact]
    public void Stop_ReleasesLeftButton()
    {
        var (macro, input, _) = Make(_ => Frame(bars: true));
        macro.Start();
        macro.Step(1000);
        macro.Step(1050);
        macro.Step(1050 + 250);                 // reel, kemungkinan LeftDown
        macro.Stop();
        Assert.False(macro.Running);
        Assert.Equal(input.DownCount, input.UpCount);
    }

    [Fact]
    public void NotForeground_PausesWithoutInput()
    {
        var (macro, input, _) = Make();
        input.Foreground = false;
        macro.Start();
        macro.Step(1000);
        Assert.Equal("Paused — focus the Roblox window (hover your fishing spot)", macro.StatusText);
        Assert.Equal(0, input.DownCount);
        macro.Stop();
    }

    [Fact]
    public void AfterMinigame_TriggersRefill_ThenReturnsToCast()
    {
        // RefillEvery=1: begitu cooldown habis → phase Refill (E ditekan, tak ada dialog → skip).
        var (macro, input, _) = Make(n => n <= 60 ? Frame(bars: true, cursorRow: 120) : Frame(bars: false),
            settings: new FishingSettings(AfterCatchMs: 700, AutoRefillBait: true, RefillEvery: 1));
        macro.Start();
        macro.Step(1000);
        macro.Step(1050);
        macro.Step(1050 + 250);                   // bite → Reel
        long last = 1050 + 250;
        for (int i = 0; i < 60; i++) { last += 15; macro.Step(last); }
        macro.Step(last + 601);                   // minigame selesai → Cooldown
        Assert.Equal(FishingPhase.Cooldown, macro.Phase);
        Assert.Equal(1, macro.Minigames);
        macro.Step(last + 601 + 701);             // cooldown habis, refill due → Refill
        Assert.Equal(FishingPhase.Refill, macro.Phase);
        int tapsBefore = input.TapCount;
        macro.Step(last + 601 + 702);             // E ditekan (tap E), tak ada dialog
        Assert.True(input.TapCount > tapsBefore, "refill harus menekan E");
        // Timeout E (3 s) → gagal → kembali CastDown.
        macro.Step(last + 601 + 702 + 3100);
        Assert.Equal(FishingPhase.CastDown, macro.Phase);
    }

    [Fact]
    public void RefillDisabled_NeverEntersRefillPhase()
    {
        var (macro, _, _) = Make(n => n <= 60 ? Frame(bars: true, cursorRow: 120) : Frame(bars: false),
            settings: new FishingSettings(AfterCatchMs: 700, AutoRefillBait: false, RefillEvery: 1));
        macro.Start();
        macro.Step(1000);
        macro.Step(1050);
        macro.Step(1050 + 250);
        long last = 1050 + 250;
        for (int i = 0; i < 60; i++) { last += 15; macro.Step(last); }
        macro.Step(last + 601);
        macro.Step(last + 601 + 701);
        Assert.Equal(FishingPhase.CastDown, macro.Phase);
    }
}
