using SupaGPO.Core.Diagnostics;
using SupaGPO.Core.Input;
using SupaGPO.Core.Roblox;
using SupaGPO.Core.Settings;
using SupaGPO.Core.Visual;

namespace SupaGPO.Core.Fishing;

public enum FishingPhase { Idle, EquipRod, CastDown, CastUp, WaitBite, Reel, Cooldown, Refill }

/// <summary>
/// Macro fishing GPO (rekonstruksi Grand Piece.exe): cast = klik singkat; tunggu bite
/// (minigame muncul sendiri, waktu tidak pasti); kendalikan garis putih menjauhi zona biru
/// dengan tahan/lepas klik; ulangi. Input hanya saat jendela Roblox foreground.
/// Self-heal equip: tap slot rod hanya saat start dan tiap timeout bite genap (toggle).
/// </summary>
public sealed class GpoFishingMacro
{
    public const int SearchPollMs = 50;
    public const int ReelPollMs = 15;
    public const int MinReelMs = 800;       // reel lebih singkat = deteksi palsu, tak dihitung
    private const int BiteSettleMs = 250;   // jeda setelah cast sebelum cek bite
    private const int ReelLostMs = 600;     // bar hilang sejauh ini = minigame selesai
    private const int ReelRelocateMs = 200; // ROI gagal → scan penuh

    private readonly IInput _input;
    private readonly IGameScreen _screen;
    private readonly Func<FishingSettings> _settings;
    private readonly Func<uint> _processId;
    private readonly Func<long> _now;
    private readonly ReelDebugDumper? _debug;
    private readonly object _gate = new();
    private readonly ReelController _controller = new();
    private readonly BaitRefillController _refill;
    private readonly GpoCharacterReader? _characterReader;
    private readonly GpoNavigator? _navigator;
    private int _refillSkipTriggers; // bait penuh → lewati sejumlah trigger refill
    private int _lastRefillMinigames = -1; // penanda: trigger minigame ini sudah diproses
    private Thread? _thread;
    private volatile bool _stop;
    private bool _leftDown;
    private PixelRect? _roi;
    private long _lastSeen, _biteSince, _reelSince, _cooldownUntil, _startedAt, _lastElapsed;
    private int _biteTimeouts;

    public event Action? StatusChanged;
    public event Action? MinigameCompleted;
    public event Action<bool>? RunningChanged;

    public GpoFishingMacro(IInput input, IGameScreen screen, Func<FishingSettings> settings,
        Func<uint> processId, Func<long>? now = null, bool autoThread = true,
        ReelDebugDumper? debug = null,
        GpoCharacterReader? characterReader = null, GpoNavigator? navigator = null)
    {
        _input = input;
        _screen = screen;
        _settings = settings;
        _processId = processId;
        _now = now ?? (() => Environment.TickCount64);
        _autoThread = autoThread;
        _debug = debug;
        _characterReader = characterReader;
        _navigator = navigator;
        _refill = new BaitRefillController(input, screen, settings, characterReader, navigator, debug);
    }

    private readonly bool _autoThread;

    private FishingSettings S => _settings();

    public bool Running { get; private set; }
    public FishingPhase Phase { get; private set; } = FishingPhase.Idle;
    public string StatusText { get; private set; } = "Fishing ready";
    public int Minigames { get; private set; }
    public double LastProgress { get; private set; }
    public TimeSpan Elapsed => TimeSpan.FromMilliseconds(Running ? Math.Max(0, _now() - _startedAt) : _lastElapsed);
    public string SessionText => $"{Elapsed:hh\\:mm\\:ss} · {Minigames} minigames";

    public void Toggle() { if (Running) Stop(); else Start(); }

    public void Start()
    {
        lock (_gate)
        {
            if (Running) return;
            Running = true;
            _stop = false;
            Minigames = 0;
            _biteTimeouts = 0;
            _refillSkipTriggers = 0;
            _lastRefillMinigames = -1;
            _lastSeen = 0;
            _roi = null;
            _controller.Reset();
            _debug?.ResetRun();
            _startedAt = _now();
            Phase = FishingPhase.Idle;
            if (_autoThread)
            {
                _thread = new Thread(Run) { IsBackground = true, Name = "GpoFishingMacro" };
                _thread.Start();
            }
        }
        RunningChanged?.Invoke(true);
        StatusChanged?.Invoke();
    }

    public void Stop()
    {
        lock (_gate)
        {
            if (!Running) return;
            _stop = true;
            _thread?.Join(1500);
            _thread = null;
            Running = false;
            _lastElapsed = _now() - _startedAt;
            ReleaseLeft();
            Phase = FishingPhase.Idle;
            SetStatus("Fishing ready");
        }
        RunningChanged?.Invoke(false);
        StatusChanged?.Invoke();
    }

    private void Run()
    {
        while (!_stop)
        {
            int sleep;
            try { sleep = Step(_now()); }
            catch (Exception ex) { AppLog.Error("Macro step", ex); sleep = 250; }
            if (_stop) break;
            Thread.Sleep(Math.Max(5, sleep));
        }
    }

    internal int Step(long now)
    {
        uint pid = _processId();
        if (pid == 0) { SetStatus("Waiting for Roblox…"); ReleaseLeft(); return 500; }

        if (!_input.IsProcessForeground(pid))
        {
            if (Phase is FishingPhase.Reel or FishingPhase.CastDown) ReleaseLeft();
            SetStatus("Paused — focus the Roblox window (hover your fishing spot)");
            return 400;
        }

        PixelRect? rectOrNull = _screen.GetClientRect((int)pid);
        if (rectOrNull is not PixelRect rect) { SetStatus("Roblox window not found"); ReleaseLeft(); return 500; }

        switch (Phase)
        {
            case FishingPhase.Idle:
                // Equip rod dulu bila diaktifkan: mulai dengan rod TIDAK dipegang,
                // karena menekan slot yang sama di GPO melepas rod (toggle).
                if (S.EquipOnStart)
                {
                    _input.TapKey((ushort)(0x30 + Math.Clamp(S.RodSlot, 1, 9)));
                    Enter(FishingPhase.CastDown, now);
                    SetStatus("Equipping rod…");
                    _debug?.Log(now, "EQUIP-START");
                    return 400;
                }
                Enter(FishingPhase.CastDown, now);
                goto case FishingPhase.CastDown;

            case FishingPhase.EquipRod:
                _input.TapKey((ushort)(0x30 + Math.Clamp(S.RodSlot, 1, 9)));
                Enter(FishingPhase.CastDown, now);
                return 400;

            case FishingPhase.CastDown:
                _input.LeftDown();
                _leftDown = true;
                Enter(FishingPhase.CastUp, now);
                return Math.Max(10, S.CastClickMs);

            case FishingPhase.CastUp:
                ReleaseLeft();
                _biteSince = now;
                Enter(FishingPhase.WaitBite, now);
                return BiteSettleMs;

            case FishingPhase.WaitBite:
            {
                if (now - _biteSince < BiteSettleMs) return SearchPollMs;
                var reading = Detect(rect, full: true);
                if (reading is not null)
                {
                    _biteTimeouts = 0;
                    _controller.Reset();
                    _lastSeen = now;
                    _reelSince = now;
                    _debug?.Log(now, $"BITE capMs={_lastCaptureMs}");
                    Enter(FishingPhase.Reel, now);
                    return StepReel(reading, now);
                }
                if (now - _biteSince > (long)S.BiteTimeoutSec * 1000)
                {
                    _biteTimeouts++;
                    _debug?.Log(now, $"TIMEOUT n={_biteTimeouts}");
                    SetStatus($"No bite after {S.BiteTimeoutSec}s — recasting ({_biteTimeouts})");
                    // Self-heal equip hanya bila user memilih toggle-equip (EquipOnStart).
                    Enter(_biteTimeouts % 2 == 0 && S.EquipOnStart ? FishingPhase.EquipRod : FishingPhase.CastDown, now);
                    return 300;
                }
                SetStatus($"Waiting for bite… {(now - _biteSince) / 1000}s");
                return SearchPollMs;
            }

            case FishingPhase.Reel:
            {
                ReelReading? reading = null;
                bool roiFailed = false;
                if (_roi is null) reading = Detect(rect, full: true);
                else (reading, roiFailed) = DetectRoi(rect);

                if (reading is null)
                {
                    ReleaseLeft();
                    if (now - _lastSeen > ReelLostMs)
                    {
                        if (now - _reelSince >= MinReelMs)
                        {
                            Minigames++;
                            MinigameCompleted?.Invoke();
                            _debug?.Log(now, $"END minigames={Minigames} lastProgress={LastProgress:F2}");
                        }
                        else
                        {
                            _debug?.Log(now, $"FALSE-POSITIVE reelMs={now - _reelSince}");
                        }
                        _cooldownUntil = now + Math.Max(500, S.AfterCatchMs);
                        Enter(FishingPhase.Cooldown, now);
                        return 100;
                    }
                    if (roiFailed && now - _lastSeen > ReelRelocateMs && Detect(rect, full: true) is { } relocated)
                    {
                        _lastSeen = now;
                        return StepReel(relocated, now);
                    }
                    return ReelPollMs;
                }
                return StepReel(reading, now);
            }

            case FishingPhase.Cooldown:
                if (now >= _cooldownUntil)
                {
                    // Isi ulang bait: tiap N minigame (counter sejak start; bait tak bisa dibaca
                    // dari memori). Bait penuh (teks merah) → lewati beberapa trigger.
                    bool due = S.AutoRefillBait && Minigames > 0 &&
                        Minigames % Math.Max(1, S.RefillEvery) == 0 && _lastRefillMinigames != Minigames;
                    if (due)
                    {
                        _lastRefillMinigames = Minigames;
                        if (_refillSkipTriggers > 0)
                            _refillSkipTriggers--;
                        else
                        {
                            _refill.Start(now);
                            Enter(FishingPhase.Refill, now);
                            _debug?.Log(now, $"REFILL-START minigames={Minigames}");
                            return 100;
                        }
                    }
                    Enter(FishingPhase.CastDown, now);
                    return 100;
                }
                SetStatus("Minigame done — recasting soon");
                return 200;

            case FishingPhase.Refill:
            {
                var (sleep, status) = _refill.Step(now, rect);
                SetStatus(status);
                if (!_refill.Finished) return sleep;
                if (_refill.FullBait) _refillSkipTriggers = 3; // backoff: coba lagi ~3 trigger nanti
                _debug?.Log(now, $"REFILL-END state={_refill.State} failed={_refill.Failed} " +
                    $"full={_refill.FullBait} minigames={Minigames}");
                Enter(FishingPhase.CastDown, now);
                return 100;
            }

            default:
                return 200;
        }
    }

    private int StepReel(ReelReading reading, long now)
    {
        _lastSeen = now;
        LastProgress = reading.Progress;
        bool onFish = reading.CursorY is not null && !reading.CursorInBlue;
        // AREA AMAN (TargetY) yang digerakkan klik; garis putih (CursorY) = target ikan.
        bool hold = _controller.Update(reading.TargetY, reading.CursorY, now);
        if (hold && !_leftDown) { _input.LeftDown(); _leftDown = true; }
        else if (!hold && _leftDown) { _input.LeftUp(); _leftDown = false; }
        int pct = (int)Math.Round(reading.Progress * 100);
        _debug?.Log(now, $"REEL zone={reading.TargetY?.ToString() ?? "-"} fish={reading.CursorY?.ToString() ?? "-"} " +
            $"offFish={!onFish} progress={reading.Progress:F2} hold={hold} capMs={_lastCaptureMs} " +
            $"holdUp={_controller.HoldMovesUp}");
        SetStatus(reading.CursorY is null
            ? $"Reeling… cursor lost ({pct}%)"
            : $"Reeling… {pct}% · {(onFish ? "on fish" : "OFF FISH")}");
        return ReelPollMs;
    }

    // ===== deteksi & ROI =====

    private long _lastCaptureMs;

    private ReelReading? Detect(PixelRect client, bool full)
    {
        PixelRect area = full ? SearchArea(client) : Clamp(_roi ?? SearchArea(client), client);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var frame = _screen.Capture(area);
        sw.Stop();
        _lastCaptureMs = sw.ElapsedMilliseconds;
        if (frame is null) return null;
        var reading = ReelBarAnalyzer.Analyze(frame, client.H);
        if (reading is not null)
        {
            int mx = Math.Max(30, client.H * 6 / 100), my = Math.Max(40, client.H * 8 / 100);
            int x0 = area.X + Math.Min(reading.LeftX, reading.RightX) - mx;
            int y0 = area.Y + Math.Min(reading.Top, reading.RightTop) - my;
            int x1 = area.X + Math.Max(reading.LeftX, reading.RightX) + mx;
            int y1 = area.Y + Math.Max(reading.Bottom, reading.RightBottom) + my;
            _roi = Clamp(new PixelRect(x0, y0, x1 - x0, y1 - y0), client);
            if (Phase == FishingPhase.Reel)
                _debug?.DumpFrame(now: Environment.TickCount64, frame, area);
        }
        return reading;
    }

    private (ReelReading? Reading, bool RoiFailed) DetectRoi(PixelRect client)
    {
        var reading = Detect(client, full: false);
        return (reading, reading is null);
    }

    private static PixelRect SearchArea(PixelRect c) =>
        new(c.X + c.W * 15 / 100, c.Y + c.H * 10 / 100, c.W * 70 / 100, c.H * 80 / 100);

    private static PixelRect Clamp(PixelRect r, PixelRect c)
    {
        int x0 = Math.Max(c.X, r.X), y0 = Math.Max(c.Y, r.Y);
        int x1 = Math.Min(c.Right, r.Right), y1 = Math.Min(c.Bottom, r.Bottom);
        return new PixelRect(x0, y0, Math.Max(8, x1 - x0), Math.Max(8, y1 - y0));
    }

    private void Enter(FishingPhase phase, long now)
    {
        Phase = phase;
        SetStatus(phase switch
        {
            FishingPhase.EquipRod => "Equipping rod…",
            FishingPhase.CastDown or FishingPhase.CastUp => "Casting…",
            FishingPhase.WaitBite => "Waiting for bite…",
            FishingPhase.Reel => "Reeling…",
            FishingPhase.Refill => "Refilling bait…",
            _ => StatusText,
        });
    }

    private void SetStatus(string text)
    {
        if (StatusText != text) { StatusText = text; StatusChanged?.Invoke(); }
    }

    private void ReleaseLeft()
    {
        if (_leftDown) { _input.LeftUp(); _leftDown = false; }
    }
}
