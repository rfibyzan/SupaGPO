using SupaGPO.Core.Offsets;

namespace SupaGPO.Core.Tests;

public class OffsetsSnapshotTests
{
    [Fact]
    public void Load_parses_groups()
    {
        var snap = OffsetsSnapshot.Load("{\"Fishing.ReelFlag\": 12345, \"Fishing.BaitCount\": 67890}");
        Assert.Equal(12345L, snap.GetLong("Fishing.ReelFlag"));
        Assert.Equal(67890, snap.GetInt("Fishing.BaitCount"));
        Assert.True(snap.Contains("Fishing.ReelFlag"));
    }

    [Fact]
    public void Missing_offset_throws()
    {
        var snap = OffsetsSnapshot.Load("{}");
        Assert.Throws<MissingOffsetException>(() => snap.GetLong("Fishing.ReelFlag"));
    }

    [Fact]
    public void GetString_roundtrips()
    {
        var snap = OffsetsSnapshot.Load("{\"Loot.LastChestRemoved\": \"chest_1\"}");
        Assert.Equal("chest_1", snap.GetString("Loot.LastChestRemoved"));
    }
}
