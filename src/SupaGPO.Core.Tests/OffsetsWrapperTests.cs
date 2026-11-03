using SupaGPO.Core.Offsets;

namespace SupaGPO.Core.Tests;

/// <summary>
/// Format wrapper provider (RbxDumperV2): offset asli = JSON string ter-escape di
/// properti "Offsets", metadata "Roblox Version" di root — persis file live
/// %APPDATA%\SupaGPO\offsets.json.
/// </summary>
public class OffsetsWrapperTests
{
    private const string WrapperJson = """
    {
      "Source": "https://offsets.imtheo.lol",
      "Roblox Version": "version-5a21b87bbc2641cf",
      "Dumper Version": "2.2.4",
      "Total Offsets": 392,
      "Offsets": "{\"TaskScheduler\":{\"Pointer\":147445400,\"MaxFPS\":176},\"FakeDataModel\":{\"Pointer\":147875328,\"RealDataModel\":504},\"Instance\":{\"ChildrenStart\":112,\"ChildrenEnd\":8,\"Parent\":96,\"ClassDescriptor\":24,\"ClassName\":8},\"Misc\":{\"StringLength\":16}}"
    }
    """;

    [Fact]
    public void Load_wrapper_flattens_nested_offsets()
    {
        var snap = OffsetsSnapshot.Load(WrapperJson);
        Assert.Equal(147445400L, snap.GetLong("TaskScheduler.Pointer"));
        Assert.Equal(176, snap.GetInt("TaskScheduler.MaxFPS"));
        Assert.Equal(112L, snap.GetLong("Instance.ChildrenStart"));
        Assert.Equal(8L, snap.GetLong("Instance.ChildrenEnd"));
    }

    [Fact]
    public void Load_wrapper_exposes_roblox_version()
    {
        var snap = OffsetsSnapshot.Load(WrapperJson);
        Assert.True(snap.HasVersionInfo);
        Assert.Equal("version-5a21b87bbc2641cf", snap.RobloxVersion);
    }

    [Fact]
    public void Load_wrapper_merged_with_flat_keys()
    {
        var snap = OffsetsSnapshot.Load(WrapperJson);
        // Nested + metadata root tetap ada; Count = 5 metadata + 8 flattened.
        Assert.True(snap.Contains("Source"));
        Assert.True(snap.Contains("Total Offsets"));
        Assert.True(snap.Contains("TaskScheduler.Pointer"));
        Assert.True(snap.Count >= 13);
    }

    [Fact]
    public void TryResolve_alias_children_start()
    {
        var snap = OffsetsSnapshot.Load(WrapperJson);
        Assert.True(snap.TryResolve("Children", out long start));
        Assert.Equal(112, start);
    }

    [Fact]
    public void TryResolve_alias_children_end()
    {
        var snap = OffsetsSnapshot.Load(WrapperJson);
        Assert.True(snap.TryResolve("ChildrenEnd", out long end));
        Assert.Equal(8, end);
    }

    [Fact]
    public void TryResolve_alias_class_descriptor_to_class_name()
    {
        var snap = OffsetsSnapshot.Load(WrapperJson);
        Assert.True(snap.TryResolve("ClassDescriptorToClassName", out long cn));
        Assert.Equal(8, cn);
        Assert.True(snap.TryResolve("ClassDescriptor", out long cd));
        Assert.Equal(24, cd);
    }

    [Fact]
    public void TryResolve_exact_wins_over_alias()
    {
        // Format flat lama: kunci "Children" exact tetap dipakai, bukan alias.
        var snap = OffsetsSnapshot.Load("{\"Children\": 64, \"Instance\": {\"ChildrenStart\": 112}}");
        Assert.True(snap.TryResolve("Children", out long start));
        Assert.Equal(64, start);
    }

    [Fact]
    public void TryResolve_dotted_key_works_with_nested_schema()
    {
        var snap = OffsetsSnapshot.Load(WrapperJson);
        Assert.True(snap.TryResolve("FakeDataModel.Pointer", out long ptr));
        Assert.Equal(147875328L, ptr);
        Assert.True(snap.TryResolve("FakeDataModel.RealDataModel", out long real));
        Assert.Equal(504L, real);
    }

    [Fact]
    public void Matches_roblox_version_equal()
    {
        var snap = OffsetsSnapshot.Load(WrapperJson);
        Assert.True(snap.MatchesRobloxVersion("version-5a21b87bbc2641cf"));
    }

    [Fact]
    public void Matches_roblox_version_case_insensitive()
    {
        var snap = OffsetsSnapshot.Load(WrapperJson);
        Assert.True(snap.MatchesRobloxVersion("VERSION-5A21B87BBC2641CF"));
    }

    [Fact]
    public void Matches_roblox_version_mismatch()
    {
        var snap = OffsetsSnapshot.Load(WrapperJson);
        Assert.False(snap.MatchesRobloxVersion("version-aaaaaaaaaaaaaaaa"));
    }

    [Fact]
    public void Matches_roblox_version_tolerant_without_version_info()
    {
        var snap = OffsetsSnapshot.Load("{\"Fishing.ReelFlag\": 1437}");
        Assert.False(snap.HasVersionInfo);
        Assert.True(snap.MatchesRobloxVersion(null));
        Assert.True(snap.MatchesRobloxVersion("version-whatever"));
    }

    [Fact]
    public void Load_wrapper_damaged_nested_is_tolerated()
    {
        var snap = OffsetsSnapshot.Load("{\"Roblox Version\":\"version-x\",\"Offsets\":\"{not json\"}");
        Assert.Equal("version-x", snap.RobloxVersion);
        // Tidak throw; kunci root tetap terbaca.
        Assert.True(snap.Contains("Offsets"));
    }
}
