using SupaGPO.Core.Input;

namespace SupaGPO.Core.Fishing;

/// <summary>
/// Cast finite state machine. Holds LMB for CastHoldMs, releases for CastReleaseMs, then recasts.
/// Injects clock and input so it can be tested/dry-run without Roblox.
/// </summary>
public sealed class CastFsm
{
    public enum State { Idle, Holding, Releasing }

    private readonly IFishingSensors _sensors;
    private readonly IInput _input;
    private readonly Func<long> _clock;

    public State Current { get; private set; } = State.Idle;
    public long StateStartedAt { get; private set; }

    public int CastHoldMs { get; set; } = 300;
    public int CastReleaseMs { get; set; } = 200;

    public Action<string, string>? Log { get; set; }
    public Action<int> Sleep { get; set; } = System.Threading.Thread.Sleep;

    public CastFsm(IFishingSensors sensors, IInput input, Func<long> clock)
    {
        _sensors = sensors;
        _input = input;
        _clock = clock;
    }

    public void Abort()
    {
        if (Current == State.Holding || Current == State.Releasing)
        {
            _input.LeftUp();
        }
        Current = State.Idle;
        StateStartedAt = 0;
    }

    public void Start(long now)
    {
        Current = State.Holding;
        StateStartedAt = now;
        _input.LeftDown();
        Log?.Invoke(FishingTexts.LoggerMousePulse, FishingTexts.StartingNormalCasting);
    }

    public FishingStatus? Tick(long now)
    {
        if (Current == State.Idle) return null;

        long elapsed = now - StateStartedAt;

        if (Current == State.Holding)
        {
            if (elapsed >= CastHoldMs)
            {
                _input.LeftUp();
                Current = State.Releasing;
                StateStartedAt = now;
                return new FishingStatus(FishingTexts.CategoryCasting, FishingTexts.NormalCasting,
                    FlagA: true, Value: 100.0);
            }
            return new FishingStatus(FishingTexts.CategoryCasting, FishingTexts.NormalCasting,
                FlagA: true, Value: (double)elapsed / CastHoldMs * 100.0);
        }

        if (Current == State.Releasing)
        {
            if (elapsed >= CastReleaseMs)
            {
                Current = State.Idle;
                StateStartedAt = 0;
                // recast immediately
                Start(now);
                return null;
            }
            return new FishingStatus(FishingTexts.CategoryCasting, FishingTexts.NormalCasting,
                FlagA: true, Value: 0.0);
        }

        return null;
    }

    public void ResetCycle()
    {
        Current = State.Idle;
        StateStartedAt = 0;
    }
}
