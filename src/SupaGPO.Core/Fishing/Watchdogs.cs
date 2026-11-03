namespace SupaGPO.Core.Fishing;

/// <summary>
/// Stall watchdog: if no reel/fishing signal for longer than StallThresholdMs, trigger recovery
/// (tap rod slot, sleep, reset). Mirrors heritage Recover_StallWatchdog.
/// </summary>
public sealed class StallWatchdog
{
    public const int DefaultStallThresholdMs = 15000;
    public const int SleepAfterTapMs = 180;

    private long _lastSignalAt;
    private bool _armed;

    public int StallThresholdMsProp { get; set; } = DefaultStallThresholdMs;

    public void Signal(long now) { _lastSignalAt = now; _armed = true; }

    public bool ShouldRecover(long now)
    {
        if (!_armed) return false;
        return (now - _lastSignalAt) >= StallThresholdMsProp;
    }

    public void Reset() { _armed = false; _lastSignalAt = 0; }
}

/// <summary>
/// Reel-hold watchdog: if LMB held too long during reel, release + backoff.
/// Mirrors heritage Reel_HoldWatchdog (hold > MaxHoldMs -> reset + backoff).
/// </summary>
public sealed class ReelHoldWatchdog
{
    public const int MaxHoldMs = 1200;
    public const int BackoffMs = 3000;

    private long _heldSince;
    private bool _held;

    public void Update(long now, bool leftHeld, long leftDownSince)
    {
        if (leftHeld && !_held)
        {
            _held = true;
            _heldSince = leftDownSince;
        }
        else if (!leftHeld)
        {
            _held = false;
            _heldSince = 0;
        }
    }

    public int? CheckBackoff(long now)
    {
        if (!_held) return null;
        if ((now - _heldSince) >= MaxHoldMs)
        {
            _held = false;
            _heldSince = 0;
            return BackoffMs;
        }
        return null;
    }

    public void Reset() { _held = false; _heldSince = 0; }
}
