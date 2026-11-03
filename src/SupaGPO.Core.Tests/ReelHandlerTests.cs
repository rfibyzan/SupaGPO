using SupaGPO.Core.Fishing;
using SupaGPO.Core.Input;

namespace SupaGPO.Core.Tests;

public class ReelHandlerTests
{
    [Fact]
    public void Holds_between_edges_taps_outside()
    {
        var input = new FakeInput();
        long now = 1000;
        var reel = new ReelHandler(input, () => now);

        reel.Tick(0.5, true);
        Assert.True(input.LeftHeld);

        now += 100;
        reel.Tick(0.95, true);
        Assert.True(input.LeftHeld);

        reel.Tick(-1, false);
        Assert.False(input.LeftHeld);
    }
}
