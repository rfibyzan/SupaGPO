using SupaGPO.Core.Camera;
using SupaGPO.Core.Fishing;
using SupaGPO.Core.Loot;
using SupaGPO.Core.Settings;

namespace SupaGPO.Core.Tests;

public class FishingLoopDryRunTests
{
    [Fact]
    public void Disabled_or_unattached_returns_null()
    {
        var input = new FakeInput();
        var sensors = new FakeSensors();
        var loop = new FishingLoop(input, sensors, new LootController(), new CameraService(),
            new AppSettings(), () => false, () => 0, () => 0);
        Assert.Null(loop.Tick());

        loop.Enabled = true;
        Assert.Null(loop.Tick()); // not attached
    }

    [Fact]
    public void Cast_starts_when_idle_and_attached()
    {
        var input = new FakeInput();
        var sensors = new FakeSensors { ReelActive = false };
        var loop = new FishingLoop(input, sensors, new LootController(), new CameraService(),
            new AppSettings { Fishing = new FishingSettings() },
            () => true, () => 1, () => Environment.TickCount64);
        loop.Enabled = true;

        var status = loop.Tick();
        Assert.NotNull(status);
        Assert.True(input.LeftHeld);
        Assert.Equal(SupaGPO.Core.Fishing.FishingTexts.CategoryCasting, status!.Category);
    }
}
