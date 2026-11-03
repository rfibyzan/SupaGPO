using SupaGPO.Core.Diagnostics;
using SupaGPO.Core.Input;
using SupaGPO.Core.Roblox;
using SupaGPO.Core.Settings;
using SupaGPO.Core.Visual;

namespace SupaGPO.Core.Fishing;

public enum RefillState
{
    Idle,
    // Navigation phases (only when AutoWalkToBait = true)
    WalkToSeller,
    WalkBackToSpot,
    // Dialog phases
    PressE,
    WaitPurchase,
    WaitQtyInput,
    WaitQtyType,
    WaitQtyBuy,
    WaitBar,
    Finished
}

/// <summary>
/// Isi ulang Common bait di Fish Hub:
/// - Jika AutoWalkToBait: jalan ke nearest bait shop → E → Yes → ketik jumlah → Buy → tutup "..." → jalan balik ke spot
/// - Jika tidak: E dari posisi mancing (harus dalam jangkauan prompt)
/// Semua klik dijaga di dalam bar dialog terdeteksi (tanpa bar = tidak klik).
/// </summary>
public sealed class BaitRefillController
{
    private const int ETimeoutMs = 3000;
    private const int QtyTimeoutMs = 3000;
    private const int BuyTimeoutMs = 3000;
    private const int BarTimeoutMs = 2500;
    private const int ActionDelayMs = 350;
    private const int BandTopPct = 65;
    private const int BandHeightPct = 31;

    private readonly IInput _input;
    private readonly IGameScreen _screen;
    private readonly Func<FishingSettings> _settings;
    private readonly GpoCharacterReader? _characterReader;
    private readonly GpoNavigator? _navigator;
    private readonly ReelDebugDumper? _debug;

    private RefillState _state = RefillState.Idle;
    private long _stateSince, _nextAction, _walkStart;
    private bool _dumped;
    private (double X, double Z) _fishingSpot;
    private (double X, double Z) _targetShop;

    public BaitRefillController(IInput input, IGameScreen screen, Func<FishingSettings> settings,
        GpoCharacterReader? characterReader = null, GpoNavigator? navigator = null,
        ReelDebugDumper? debug = null)
    {
        _input = input;
        _screen = screen;
        _settings = settings;
        _characterReader = characterReader;
        _navigator = navigator;
        _debug = debug;
    }

    public RefillState State => _state;
    public bool Finished => _state == RefillState.Finished;
    public bool Failed { get; private set; }
    public bool FullBait { get; private set; }

    public void Start(long now)
    {
        var s = _settings();
        _state = s.AutoWalkToBait && _navigator is not null && _characterReader is not null
            ? RefillState.WalkToSeller
            : RefillState.PressE;

        _stateSince = now;
        _nextAction = now;
        _dumped = false;
        Failed = false;
        FullBait = false;
        _walkStart = now;

        // Save fishing spot for return trip
        if (_characterReader?.HasData == true)
        {
            var pos = _characterReader.Position!.Value;
            _fishingSpot = (pos.X, pos.Z);
        }
        else
        {
            _fishingSpot = (0, 0);
        }
    }

    public (int SleepMs, string Status) Step(long now, PixelRect client)
    {
        var s = _settings();

        if (_state is RefillState.Idle or RefillState.Finished)
            return (200, Failed ? "Refill skipped — continuing fishing" : "Refill done — continuing fishing");

        // Global walk timeout
        if ((_state == RefillState.WalkToSeller || _state == RefillState.WalkBackToSpot) &&
            now - _walkStart > s.WalkTimeoutSec * 1000)
        {
            _navigator?.ReleaseAll();
            Fail($"Walk timeout at {_state}");
            return (100, "Refill skipped — walk timeout, continuing fishing");
        }

        // Per-state timeout for dialog phases
        long limit = _state switch
        {
            RefillState.PressE => ETimeoutMs,
            RefillState.WaitPurchase => ETimeoutMs,
            RefillState.WaitQtyInput or RefillState.WaitQtyType => QtyTimeoutMs,
            RefillState.WaitQtyBuy => BuyTimeoutMs,
            RefillState.WaitBar => BarTimeoutMs,
            _ => 0,
        };
        if (limit > 0 && now - _stateSince > limit)
        {
            if (_state == RefillState.WaitBar) { Finish(); return (100, "Refill done — continuing fishing"); }
            _navigator?.ReleaseAll();
            Fail($"Refill timed out at {_state}");
            return (100, "Refill skipped — dialog not detected, continuing fishing");
        }

        if (now < _nextAction) return (50, StatusFor());

        switch (_state)
        {
            case RefillState.WalkToSeller:
                return StepWalkToSeller(now, client);

            case RefillState.WalkBackToSpot:
                return StepWalkBack(now, client);

            case RefillState.PressE:
                _input.TapKey(0x45); // E
                Transition(RefillState.WaitPurchase, now, ActionDelayMs);
                return (ActionDelayMs, "Opening bait dialog…");

            case RefillState.WaitPurchase:
            case RefillState.WaitQtyInput:
            case RefillState.WaitQtyType:
            case RefillState.WaitQtyBuy:
            case RefillState.WaitBar:
                return ScanAndAct(now, client);

            default:
                return (100, StatusFor());
        }
    }

    private (int SleepMs, string Status) StepWalkToSeller(long now, PixelRect client)
    {
        if (_navigator is null || _characterReader is null)
        { Transition(RefillState.PressE, now, 0); return (0, "Nav not ready — pressing E"); }

        // Find nearest bait shop from current position (or fishing spot)
        double fromX = _characterReader.Position?.X ?? _fishingSpot.X;
        double fromZ = _characterReader.Position?.Z ?? _fishingSpot.Z;
        _targetShop = BaitShopPositions.Nearest(fromX, fromZ);

        _navigator.WalkTo(_targetShop.X, _targetShop.Z, arriveRadius: 3.5);
        bool arrived = _navigator.Update(now);

        if (arrived)
        {
            _navigator.ReleaseAll();
            Transition(RefillState.PressE, now, 500); // small delay after arriving
            return (500, "Arrived at bait shop — pressing E");
        }

        return (50, $"Walking to bait shop ({_targetShop.X:0},{_targetShop.Z:0})…");
    }

    private (int SleepMs, string Status) StepWalkBack(long now, PixelRect client)
    {
        if (_navigator is null || _fishingSpot == (0, 0))
        { Finish(); return (100, "Refill done — continuing fishing"); }

        _navigator.WalkTo(_fishingSpot.X, _fishingSpot.Z, arriveRadius: 4.0);
        bool arrived = _navigator.Update(now);

        if (arrived)
        {
            _navigator.ReleaseAll();
            Finish();
            return (100, "Returned to fishing spot — continuing fishing");
        }

        return (50, $"Returning to fishing spot ({_fishingSpot.X:0},{_fishingSpot.Z:0})…");
    }

    private (int SleepMs, string Status) ScanAndAct(long now, PixelRect client)
    {
        var (scan, frame, band) = Scan(client);
        if (frame is not null && !_dumped) { _debug?.DumpFrame(now, frame, band); _dumped = true; }
        if (scan is null) return (80, StatusFor());

        // Red text "maximum amount" → close bar, backoff.
        if (scan.RedText)
        {
            CloseBar(scan, client, now);
            FullBait = true;
            Finish();
            return (100, "Bait is full — refill backed off");
        }

        if (!scan.Bar)
        {
            if (_state == RefillState.WaitBar) { Finish(); return (100, "Refill done — continuing fishing"); }
            Fail($"Dialog closed at {_state}");
            return (100, "Refill skipped — dialog closed, continuing fishing");
        }

        switch (_state)
        {
            case RefillState.WaitPurchase:
            {
                if (scan.Buttons.Count < 1) return (80, "Waiting for Yes button…");
                if (!Click(scan, client, Center(scan.Buttons[0].Rect)))
                { Fail("Yes click outside bar"); return (100, "Refill skipped"); }
                Transition(RefillState.WaitQtyInput, now, ActionDelayMs);
                return (ActionDelayMs, "Confirmed purchase — entering amount…");
            }

            case RefillState.WaitQtyInput:
            {
                if (scan.Buttons.Count < 1) return (80, "Waiting for amount input…");
                if (!Click(scan, client, Center(scan.Buttons[0].Rect)))
                { Fail("Input click outside bar"); return (100, "Refill skipped"); }
                Transition(RefillState.WaitQtyType, now, ActionDelayMs);
                return (ActionDelayMs, "Focusing amount input…");
            }

            case RefillState.WaitQtyType:
            {
                string amt = Math.Clamp(_amount(), 1, 999).ToString();
                foreach (char c in amt) _input.TapKey((ushort)(0x30 + (c - '0')));
                _debug?.Log(now, $"REFILL type amount={amt}");
                Transition(RefillState.WaitQtyBuy, now, ActionDelayMs);
                return (ActionDelayMs, $"Typed {amt} — pressing Buy…");
            }

            case RefillState.WaitQtyBuy:
            {
                if (scan.Buttons.Count < 2)
                { Fail("Buy button not found"); return (100, "Refill skipped"); }
                if (!Click(scan, client, Center(scan.Buttons[1].Rect)))
                { Fail("Buy click outside bar"); return (100, "Refill skipped"); }
                _debug?.Log(now, $"REFILL buy buttons={scan.Buttons.Count} " +
                    string.Join(",", scan.Buttons.Select(b => $"{b.Rect.X},{b.Rect.Y}")));
                Transition(RefillState.WaitBar, now, ActionDelayMs);
                return (ActionDelayMs, "Bought — closing text bar…");
            }

            case RefillState.WaitBar:
                CloseBar(scan, client, now);
                // After buying, walk back if we walked to seller
                if (_settings().AutoWalkToBait && _navigator is not null)
                {
                    Transition(RefillState.WalkBackToSpot, now, 500);
                    return (500, "Purchase done — returning to fishing spot");
                }
                Finish();
                return (100, "Refill done — continuing fishing");

            default:
                return (100, StatusFor());
        }
    }

    private void CloseBar(DialogScan scan, PixelRect client, long now)
    {
        (int X, int Y) pt = scan.Buttons.Count > 0
            ? Center(scan.Buttons[^1].Rect)
            : (scan.BarRect.Right - Math.Max(8, scan.BarRect.W / 40), scan.BarRect.Y + scan.BarRect.H / 2);
        Click(scan, client, pt);
        _debug?.Log(now, $"REFILL close-bar at {pt.X},{pt.Y} buttons={scan.Buttons.Count}");
    }

    private (DialogScan? Scan, ColorFrame? Frame, PixelRect Band) Scan(PixelRect client)
    {
        var band = new PixelRect(client.X, client.Y + client.H * BandTopPct / 100,
            client.W, client.H * BandHeightPct / 100);
        var frame = _screen.Capture(band);
        if (frame is null) return (null, null, band);
        var scan = DialogDetector.Analyze(frame, band.X, band.Y);
        return (scan, frame, band);
    }

    private bool Click(DialogScan scan, PixelRect client, (int X, int Y) screenPt)
    {
        var b = scan.BarRect;
        if (screenPt.X < b.X || screenPt.X >= b.Right || screenPt.Y < b.Y || screenPt.Y >= b.Bottom)
            return false;
        return _input.ClickAtClientPoint(_screen.Handle, screenPt.X - client.X, screenPt.Y - client.Y);
    }

    private static (int X, int Y) Center(PixelRect r) => (r.X + r.W / 2, r.Y + r.H / 2);

    private void Transition(RefillState next, long now, int delayMs)
    {
        _state = next;
        _stateSince = now;
        _nextAction = now + delayMs;
        _dumped = false;
    }

    private void Finish()
    {
        _navigator?.ReleaseAll();
        _state = RefillState.Finished;
    }

    private void Fail(string why)
    {
        _navigator?.ReleaseAll();
        Failed = true;
        _state = RefillState.Finished;
        _debug?.Log(Environment.TickCount64, $"REFILL FAIL {why}");
    }

    private Func<int> _amount => () => _settings().RefillAmount;

    private string StatusFor() => _state switch
    {
        RefillState.WalkToSeller => "Refill: walking to bait shop…",
        RefillState.WalkBackToSpot => "Refill: returning to fishing spot…",
        RefillState.PressE => "Refill: pressing E…",
        RefillState.WaitPurchase => "Refill: waiting for purchase dialog…",
        RefillState.WaitQtyInput => "Refill: waiting for amount input…",
        RefillState.WaitQtyType => "Refill: typing amount…",
        RefillState.WaitQtyBuy => "Refill: waiting for Buy…",
        RefillState.WaitBar => "Refill: closing text bar…",
        _ => "Refill",
    };
}