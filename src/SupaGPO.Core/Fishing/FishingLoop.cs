using SupaGPO.Core.Camera;
using SupaGPO.Core.Input;
using SupaGPO.Core.Loot;
using SupaGPO.Core.Net;
using SupaGPO.Core.Settings;

namespace SupaGPO.Core.Fishing;

/// <summary>
/// Fishing orchestrator. One tick per cycle:
///   focus check -> suspend check -> reel -> catch read -> cast (Normal pulse)
/// All dependencies (input, sensors, clock, settings) injectable for dry-run tests.
/// </summary>
public sealed class FishingLoop : IDisposable
{
    private readonly IInput _input;
    private readonly IFishingSensors _sensors;
    private readonly LootController _loot;
    private readonly CameraService _camera;
    private readonly CastFsm _cast;
    private readonly MousePulse _pulse;
    private readonly ShakeAssistant _shake;
    private readonly ReelHandler _reel;
    private readonly StallWatchdog _stall = new();
    private readonly ReelHoldWatchdog _holdWatchdog = new();
    private readonly WebhookDispatcher? _webhook;
    private readonly AppSettings _settings;
    private readonly Func<bool> _isAttached;
    private readonly Func<uint> _processId;
    private readonly Func<long> _clock;
    private readonly Action<int> _sleep;

    private bool _enabled;
    private bool _suspended;
    private long _castStartedAt;
    private long _lastSignalAt;
    private bool _completionActive;
    private long _completionHoldUntil;
    private bool _focusLostLogged;
    private bool _suspendedLogged;
    private bool _reelWasActive;
    private double _lastReelProgress = -1;
    private CatchOutcome _pendingOutcome = CatchOutcome.Unknown;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (value && !_enabled) Start();
            if (!value && _enabled) Stop();
            _enabled = value;
        }
    }

    public bool Suspended
    {
        get => _suspended;
        set
        {
            if (value != _suspended)
            {
                _suspended = value;
                _suspendedLogged = false;
            }
        }
    }

    public bool AutoCastEnabled { get; set; } = true;
    public bool AutoReelEnabled { get; set; } = true;
    public bool AutoShakeEnabled { get; set; } = true;

    public int RodSlot { get => _rodSlot; set => _rodSlot = Math.Clamp(value, 1, 9); }
    private int _rodSlot = 1;

    public Action<FishingStatus>? StatusPublished { get; set; }
    public Action<string, string>? Log { get; set; }
    public Action<CatchInfo>? CatchDetected { get; set; }

    public FishingStatus? LastStatus { get; private set; }
    public MousePulse Pulse => _pulse;
    public CastFsm Cast => _cast;
    public ReelHandler Reel => _reel;
    public StallWatchdog Stall => _stall;

    public FishingLoop(IInput input, IFishingSensors sensors, LootController loot,
        CameraService camera, AppSettings settings,
        Func<bool> isAttached, Func<uint> processId, Func<long> clock,
        WebhookDispatcher? webhook = null, Action<int>? sleep = null)
    {
        _input = input;
        _sensors = sensors;
        _loot = loot;
        _camera = camera;
        _settings = settings;
        _webhook = webhook;
        _isAttached = isAttached;
        _processId = processId;
        _clock = clock;
        _sleep = sleep ?? System.Threading.Thread.Sleep;

        _cast = new CastFsm(sensors, input, clock);
        _pulse = new MousePulse(input.LeftDown, input.LeftUp, clock);
        _shake = new ShakeAssistant();
        _reel = new ReelHandler(input, clock);

        _cast.Log = (cat, msg) => Log?.Invoke(cat, msg);
        _cast.Sleep = _sleep;

        _cast.CastHoldMs = settings.Fishing.CastClickMs;
        _cast.CastReleaseMs = 0;
    }

    public void Start()
    {
        _castStartedAt = 0;
        _completionActive = false;
        _completionHoldUntil = 0;
        _reelWasActive = false;
        _focusLostLogged = false;
        _suspendedLogged = false;
        _lastSignalAt = _clock();
        _stall.Signal(_lastSignalAt);
    }

    public void Stop()
    {
        _pulse.Stop();
        _input.ReleaseAllButtons();
        _cast.Abort();
        _reel.Stop();
        _completionActive = false;
        _completionHoldUntil = 0;
        _castStartedAt = 0;
        _reelWasActive = false;
    }

    public void Dispose() => Stop();

    public FishingStatus? Tick()
    {
        if (!_enabled || !_isAttached())
            return null;

        long now = _clock();
        _stall.Signal(now);

        // Focus check.
        uint pid = _processId();
        if (pid != 0 && !_input.IsProcessForeground(pid))
        {
            _pulse.Stop();
            _input.ReleaseAllButtons();
            _cast.Abort();
            _reel.Stop();
            _completionActive = false;
            if (!_focusLostLogged)
            {
                _focusLostLogged = true;
                Log?.Invoke(FishingTexts.LoggerRecover, FishingTexts.RobloxLostFocus);
            }
            return Publish(new FishingStatus(FishingTexts.CategoryPassive,
                FishingTexts.WaitingForRobloxFocus, FlagA: true));
        }
        _focusLostLogged = false;

        if (_suspended)
        {
            _pulse.Stop();
            _input.ReleaseAllButtons();
            _cast.Abort();
            _reel.Stop();
            _completionActive = false;
            if (!_suspendedLogged)
            {
                _suspendedLogged = true;
                Log?.Invoke(FishingTexts.LoggerRecover, "Suspended.");
            }
            return null;
        }
        _suspendedLogged = false;

        // Loot tick (AutoOpenChests / AutoStoreFruits).
        var lootStatus = _loot.Tick(new FishingState
        {
            ReelActive = _sensors.ReelActive,
            AutoOpenChests = _sensors.AutoOpenChests,
            AutoStoreFruits = _sensors.AutoStoreFruits,
            Fruits = _sensors.Fruits,
            LastChestRemoved = _sensors.LastChestRemoved
        });
        if (lootStatus is not null)
        {
            Log?.Invoke(FishingTexts.LoggerLoot, lootStatus.Text);
            return Publish(lootStatus);
        }

        // Camera observe.
        var cam = _sensors.CameraPosition;
        if (cam is { } c)
            _camera.Observe(c.X, c.Y, c.Z);

        // Shake assist.
        if (AutoShakeEnabled)
        {
            var shake = _shake.Tick(false, _input, _clock);
            if (shake is not null)
                return Publish(shake);
        }

        // Reel handling.
        bool reelActive = _sensors.ReelActive;
        double progress = reelActive ? _lastReelProgress : -1;
        double reelResult = _reel.Tick(progress, reelActive);

        if (reelActive && !_reelWasActive)
        {
            // New reel started.
            _castStartedAt = 0;
            _completionActive = false;
            _pulse.Stop();
            _lastReelProgress = -1;
            Log?.Invoke(FishingTexts.LoggerMousePulse, FishingTexts.ReelDetected);
        }
        _reelWasActive = reelActive;

        if (reelResult >= 0)
        {
            _lastReelProgress = reelResult;
            if (reelResult >= _reel.ReadyThreshold && !_completionActive)
            {
                // Reel complete -> catch read.
                _completionActive = true;
                _completionHoldUntil = now + 800;
                _pendingOutcome = CatchOutcomeFromProgress(reelResult);
                return Publish(new FishingStatus(FishingTexts.CategoryCasting,
                    FishingTexts.CatchCompleteWaiting, FlagA: true, Value: reelResult));
            }
            return Publish(new FishingStatus(FishingTexts.CategoryCasting, FishingTexts.ReelDetected,
                FlagA: true, Value: reelResult));
        }

        // Reel ended.
        if (_reelWasActive && !reelActive)
        {
            _reel.Stop();
            _completionActive = false;
            _castStartedAt = 0;
        }

        // Completion hold before recast.
        if (_completionActive)
        {
            if (now < _completionHoldUntil)
                return Publish(new FishingStatus(FishingTexts.CategoryCasting,
                    FishingTexts.WaitingBeforeRecast, FlagA: true));
            _completionActive = false;
        }

        // Stall recovery.
        long lastSignal = Math.Max(_lastSignalAt, _shake.LastPromptSeen);
        if (_stall.ShouldRecover(now))
        {
            _stall.Signal(now);
            _pulse.Stop();
            _cast.Abort();
            _input.ReleaseAllButtons();
            _reel.Stop();
            _input.TapKey((ushort)('0' + RodSlot));
            _sleep(StallWatchdog.SleepAfterTapMs);
            Log?.Invoke(FishingTexts.LoggerRecover, FishingTexts.RuntimeSignalStalled);
            return Publish(new FishingStatus(FishingTexts.CategoryRecover,
                FishingTexts.RuntimeSignalStalled, FlagA: true));
        }

        // Cast (Normal pulse) when not reeling and auto-cast enabled.
        if (!reelActive && AutoCastEnabled && !_completionActive)
        {
            if (_pulse.Mode == MousePulseMode.Off)
            {
                _cast.Abort();
                _pulse.StartNormalCast(now);
                _castStartedAt = now;
                Log?.Invoke(FishingTexts.LoggerMousePulse, FishingTexts.StartingNormalCasting);
            }
            return Publish(new FishingStatus(FishingTexts.CategoryCasting,
                FishingTexts.NormalCasting, FlagA: true));
        }

        return Publish(new FishingStatus(FishingTexts.CategoryPassive,
            FishingTexts.WaitingForManualCast, FlagA: true));
    }

    private static CatchOutcome CatchOutcomeFromProgress(double progress) =>
        progress >= 80 ? CatchOutcome.Caught : progress <= 20 ? CatchOutcome.Lost : CatchOutcome.Unknown;

    private FishingStatus Publish(FishingStatus status)
    {
        LastStatus = status;
        StatusPublished?.Invoke(status);
        return status;
    }
}

public sealed record CatchInfo(string FishName, string? Mutation, bool Shiny, bool Sparkling,
    string SizeText, CatchOutcome Outcome);
