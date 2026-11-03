namespace SupaGPO.Core.Fishing;

/// <summary>
/// Humanised mouse pulse: taps LMB at a configurable period with jitter.
/// Mode 1 = Normal cast pulse; Mode 2 = Requiem skip pulse (45 ms).
/// Thread-safe: pulse can be driven from a 5 ms timer while FishingLoop ticks on another thread.
/// </summary>
public enum MousePulseMode { Off, NormalCast, RequiemSkip }

public sealed class MousePulse
{
    public const int RequiemPeriodMs = 45;

    private readonly Action _leftDown;
    private readonly Action _leftUp;
    private readonly Func<long> _clock;
    private readonly object _lock = new();

    private MousePulseMode _mode;
    private long _nextAt;
    private long _startedAt;

    public MousePulseMode Mode
    {
        get { lock (_lock) return _mode; }
        private set { lock (_lock) _mode = value; }
    }

    public long StartedAt
    {
        get { lock (_lock) return _startedAt; }
    }

    public bool LeftHeld { get; private set; }

    public MousePulse(Action leftDown, Action leftUp, Func<long> clock)
    {
        _leftDown = leftDown;
        _leftUp = leftUp;
        _clock = clock;
    }

    public void StartNormalCast(long now, int periodMs = 350)
    {
        lock (_lock)
        {
            _mode = MousePulseMode.NormalCast;
            _startedAt = now;
            _nextAt = now + periodMs;
            if (!LeftHeld) { _leftDown(); LeftHeld = true; }
        }
    }

    public void StartRequiemSkip(long now)
    {
        lock (_lock)
        {
            _mode = MousePulseMode.RequiemSkip;
            _startedAt = now;
            _nextAt = now + RequiemPeriodMs;
            if (!LeftHeld) { _leftDown(); LeftHeld = true; }
        }
    }

    public void Stop()
    {
        lock (_lock)
        {
            _mode = MousePulseMode.Off;
            _nextAt = 0;
            _startedAt = 0;
            if (LeftHeld) { _leftUp(); LeftHeld = false; }
        }
    }

    /// <summary>Call each tick (~5 ms). Returns true when LMB state changed.</summary>
    public bool Tick(long now)
    {
        lock (_lock)
        {
            if (_mode == MousePulseMode.Off) return false;
            if (now < _nextAt) return false;
            _nextAt = now + (_mode == MousePulseMode.RequiemSkip ? RequiemPeriodMs : 350);
            if (LeftHeld) { _leftUp(); LeftHeld = false; return true; }
            _leftDown(); LeftHeld = true; return true;
        }
    }
}
