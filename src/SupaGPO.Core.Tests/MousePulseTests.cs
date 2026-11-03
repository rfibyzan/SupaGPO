using SupaGPO.Core.Fishing;

namespace SupaGPO.Core.Tests;

public class MousePulseTests
{
    [Fact]
    public void Tick_toggles_left_held_at_period()
    {
        var input = new FakeInput();
        long now = 1000;
        var pulse = new MousePulse(input.LeftDown, input.LeftUp, () => now);
        pulse.StartNormalCast(now, periodMs: 50);

        // Not yet due (nextAt = 1050).
        Assert.False(pulse.Tick(now));
        Assert.True(input.LeftHeld);

        // Advance past due -> release.
        now = 1060;
        Assert.True(pulse.Tick(now));
        Assert.False(input.LeftHeld);

        // Advance again -> press.
        now = 1420;
        Assert.True(pulse.Tick(now));
        Assert.True(input.LeftHeld);
    }

    [Fact]
    public void Stop_releases_and_turns_off()
    {
        var input = new FakeInput();
        var pulse = new MousePulse(input.LeftDown, input.LeftUp, () => 0L);
        pulse.StartNormalCast(0);
        pulse.Stop();
        Assert.Equal(MousePulseMode.Off, pulse.Mode);
        Assert.False(input.LeftHeld);
    }
}
