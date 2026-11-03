using SupaGPO.Core.Roblox;

namespace SupaGPO.Core.Tests;

public class InstanceDescendantSearchTests
{
    private static readonly Dictionary<long, List<long>> ChildrenMap = new()
    {
        { 1, new() { 2, 3 } },
        { 2, new() { 4 } },
        { 3, new() { 5 } },
        { 4, new() { } },
        { 5, new() { } },
    };

    private static readonly Dictionary<long, string> ClassMap = new()
    {
        { 1, "DataModel" },
        { 2, "Players" },
        { 3, "Workspace" },
        { 4, "PlayerGui" },
        { 5, "Terrain" },
    };

    [Fact]
    public void Find_descendant_by_class()
    {
        long result = InstanceDescendantSearch.FindFirstDescendantOfClass(
            1, "PlayerGui",
            IsValid, GetChildren, GetClassName);
        Assert.Equal(4, result);
    }

    [Fact]
    public void Root_is_tested_too()
    {
        long result = InstanceDescendantSearch.FindFirstDescendantOfClass(
            1, "DataModel",
            IsValid, GetChildren, GetClassName);
        Assert.Equal(1, result);
    }

    [Fact]
    public void Invalid_root_returns_zero()
    {
        long result = InstanceDescendantSearch.FindFirstDescendantOfClass(
            999, "PlayerGui",
            IsValid, GetChildren, GetClassName);
        Assert.Equal(0, result);
    }

    private static bool IsValid(long addr) => addr > 0;
    private static IEnumerable<long> GetChildren(long addr) =>
        ChildrenMap.TryGetValue(addr, out var c) ? c : Array.Empty<long>();
    private static string GetClassName(long addr) =>
        ClassMap.TryGetValue(addr, out var c) ? c : "";
}
