using SupaGPO.Core.Fishing;

namespace SupaGPO.Core.Tests;

public class SpawnNoticeParserTests
{
    [Fact]
    public void Fruit_spawn_with_location()
    {
        var n = SpawnNoticeParser.ParseLine("A fruit has spawned at Tow Town!");
        Assert.Equal("fruit-spawn", n.Kind);
        Assert.Equal("Tow Town", n.Location);
        Assert.Equal("Fruit spawned at Tow Town", n.Canonical);
    }

    [Fact]
    public void Fruit_spawn_case_insensitive()
    {
        var n = SpawnNoticeParser.ParseLine("a FRUIT has SPAWNED at Shell's Town.");
        Assert.Equal("fruit-spawn", n.Kind);
        Assert.Equal("Shell's Town", n.Location);
    }

    [Fact]
    public void Fruit_spawn_strips_trailing_punctuation()
    {
        var n = SpawnNoticeParser.ParseLine("A fruit has spawned at Rovo Island…");
        Assert.Equal("Rovo Island", n.Location);
    }

    [Fact]
    public void Fruit_spawn_without_location()
    {
        var n = SpawnNoticeParser.ParseLine("A fruit has spawned.");
        Assert.Equal("fruit-spawn", n.Kind);
        Assert.Null(n.Location);
        Assert.Equal("Fruit spawned", n.Canonical);
    }

    [Fact]
    public void Fruit_spawn_empty_location_becomes_null()
    {
        var n = SpawnNoticeParser.ParseLine("A fruit has spawned at .");
        Assert.Equal("fruit-spawn", n.Kind);
        Assert.Null(n.Location);
        Assert.Equal("Fruit spawned", n.Canonical);
    }

    [Fact]
    public void Unknown_line_passes_through_raw()
    {
        var n = SpawnNoticeParser.ParseLine("Player123: wanna trade?");
        Assert.Equal("raw", n.Kind);
        Assert.Equal("Player123: wanna trade?", n.Canonical);
    }

    [Fact]
    public void Blank_line_is_empty()
    {
        var n = SpawnNoticeParser.ParseLine("   ");
        Assert.Equal("empty", n.Kind);
        Assert.Equal(string.Empty, n.Canonical);
    }

    [Fact]
    public void ParseBatch_splits_pipe_and_newlines()
    {
        var result = SpawnNoticeParser.ParseBatch(
            "A fruit has spawned at Tow Town!\r\nPlayer123: hi|A fruit has spawned.");
        Assert.Equal(new[] { "Fruit spawned at Tow Town", "Player123: hi", "Fruit spawned" }, result);
    }

    [Fact]
    public void ParseBatch_empty_or_null_is_empty()
    {
        Assert.Empty(SpawnNoticeParser.ParseBatch(null!));
        Assert.Empty(SpawnNoticeParser.ParseBatch(""));
        Assert.Empty(SpawnNoticeParser.ParseBatch("   \n  "));
    }

    [Fact]
    public void ParseBatch_ignores_blank_entries()
    {
        var result = SpawnNoticeParser.ParseBatch("A fruit has spawned at A|  |\n|B line");
        Assert.Equal(2, result.Length);
        Assert.Equal("Fruit spawned at A", result[0]);
        Assert.Equal("B line", result[1]);
    }
}
