using SupaGPO.Core.Offsets;

namespace SupaGPO.Core.Tests;

public static class OffsetTestExtensions
{
    public static bool GetBoolean(this OffsetsSnapshot snap, string key)
    {
        var v = snap.TryGet(key, out var val) ? val : throw new MissingOffsetException(key);
        return Convert.ToBoolean(v);
    }
}

public class OffsetsFileRoundTripTests
{
    [Fact]
    public void Serialize_deserialize_roundtrip()
    {
        var snap = OffsetsSnapshot.Load("{\"Fishing.ReelFlag\": 1437, \"Fishing.BaitCount\": 1440, \"Loot.AutoOpenChests\": true}");
        var json = snap.Serialize();
        var again = OffsetsSnapshot.Load(json);
        Assert.Equal(1437L, again.GetLong("Fishing.ReelFlag"));
        Assert.Equal(1440, again.GetInt("Fishing.BaitCount"));
        Assert.True(again.GetBoolean("Loot.AutoOpenChests"));
        Assert.Equal(3, again.Count);
    }

    [Fact]
    public void Save_to_file_and_load_back()
    {
        var path = Path.Combine(Path.GetTempPath(), $"supagpo-test-{Guid.NewGuid():N}.json");
        try
        {
            var snap = OffsetsSnapshot.Load("{\"Fishing.ReelFlag\": 999}");
            snap.SaveToFile(path);
            Assert.True(File.Exists(path));

            var loaded = OffsetsSnapshot.LoadFromFile(path);
            Assert.NotNull(loaded);
            Assert.Equal(999L, loaded!.GetLong("Fishing.ReelFlag"));

            // Missing file returns null.
            var missing = OffsetsSnapshot.LoadFromFile(Path.Combine(Path.GetTempPath(), "does-not-exist.json"));
            Assert.Null(missing);
        }
        finally
        {
            try { File.Delete(path); } catch { }
        }
    }
}
