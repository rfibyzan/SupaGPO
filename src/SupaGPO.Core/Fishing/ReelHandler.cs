using SupaGPO.Core.Input;

namespace SupaGPO.Core.Fishing;

/// <summary>
/// Reel control: drives LMB during the reel minigame based on progress, with edge thresholds
/// (0.1 / 0.9) and a minimal 55 ms clicker debounce. Mirrors heritage GenericReelController.
/// </summary>
public enum ReelMode { Generic, Bellona }

public sealed class ReelHandler
{
    private readonly IInput _input;
    private readonly Func<long> _clock;

    public ReelMode Mode { get; set; } = ReelMode.Generic;
    public bool WasActive { get; private set; }
    public double ReadyThreshold { get; set; } = 99.5;

    private bool _active;
    private bool _inputLeftHeld;
    private long _lastClickAt;
    private const int ClickerDebounceMs = 55;

    public ReelHandler(IInput input, Func<long> clock)
    {
        _input = input;
        _clock = clock;
    }

    public void Stop()
    {
        _active = false;
        WasActive = _active;
        _inputLeftHeld = false;
        _input.LeftUp();
    }

    public double Tick(double progress, bool reelActive)
    {
        if (!reelActive)
        {
            Stop();
            return -1;
        }

        _active = true;
        WasActive = true;
        long now = _clock();

        bool hold = progress > 0.1 && progress < 0.9;
        if (hold)
        {
            if (!_inputLeftHeld) { _input.LeftDown(); _inputLeftHeld = true; }
        }
        else
        {
            if (_inputLeftHeld && (now - _lastClickAt) >= ClickerDebounceMs)
            {
                _input.LeftUp();
                _input.LeftDown();
                _lastClickAt = now;
            }
            else if (!_inputLeftHeld)
            {
                _input.LeftDown();
                _inputLeftHeld = true;
                _lastClickAt = now;
            }
        }
        return progress;
    }

    public void Reset() => Stop();
}
