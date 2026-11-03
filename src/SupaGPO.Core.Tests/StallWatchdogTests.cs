using SupaGPO.Core.Fishing;

namespace SupaGPO.Core.Tests;

public class StallWatchdogTests
{
    [Fact]
    public void ShouldRecover_after_threshold()
    {
        var wd = new StallWatchdog { StallThresholdMsProp = 1000 };
        wd.Signal(1000);
        Assert.False(wd.ShouldRecover(1500));
        Assert.True(wd.ShouldRecover(2001));
    }

    [Fact]
    public void Not_armed_until_signal()
    {
        var wd = new StallWatchdog { StallThresholdMsProp = 1000 };
        Assert.False(wd.ShouldRecover(999999));
    }
}
