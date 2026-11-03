using SupaGPO.Core.Fishing;

namespace SupaGPO.Core.Tests;

public class CastFsmTests
{
    private static readonly FakeInput Input = new();
    private static readonly FakeSensors Sensors = new();

    [Fact]
    public void Start_holds_then_releases_then_recasts()
    {
        var cast = new CastFsm(Sensors, Input, () => Environment.TickCount64) { CastHoldMs = 10, CastReleaseMs = 10 };
        long now = Environment.TickCount64;
        cast.Start(now);

        Assert.Equal(CastFsm.State.Holding, cast.Current);
        Assert.True(Input.LeftHeld);

        // Advance past hold -> releasing, LMB up.
        var status = cast.Tick(now + 20);
        Assert.Equal(CastFsm.State.Releasing, cast.Current);
        Assert.False(Input.LeftHeld);

        // Advance past release -> idle, recast (holding again).
        status = cast.Tick(now + 40);
        Assert.Equal(CastFsm.State.Holding, cast.Current);
        Assert.True(Input.LeftHeld);
    }

    [Fact]
    public void Abort_releases_and_idles()
    {
        var cast = new CastFsm(Sensors, Input, () => Environment.TickCount64);
        cast.Start(Environment.TickCount64);
        cast.Abort();
        Assert.Equal(CastFsm.State.Idle, cast.Current);
        Assert.False(Input.LeftHeld);
    }
}
