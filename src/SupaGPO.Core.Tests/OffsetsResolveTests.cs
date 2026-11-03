using SupaGPO.Core.Offsets;

namespace SupaGPO.Core.Tests;

public class OffsetsResolveTests
{
    [Fact]
    public void TryResolve_exact_key()
    {
        var snap = OffsetsSnapshot.Load("{\"FakeDataModel.Pointer\": 4096, \"RealDataModel\": 8192}");
        Assert.True(snap.TryResolve("FakeDataModel.Pointer", out long off));
        Assert.Equal(4096, off);
    }

    [Fact]
    public void TryResolve_dot_notation_suffix()
    {
        var snap = OffsetsSnapshot.Load("{\"Pointer\": 4096, \"RealDataModel\": 8192}");
        Assert.True(snap.TryResolve("FakeDataModel.Pointer", out long off));
        Assert.Equal(4096, off);
    }

    [Fact]
    public void TryResolve_missing_returns_false()
    {
        var snap = OffsetsSnapshot.Load("{\"Pointer\": 4096}");
        Assert.False(snap.TryResolve("Missing.Key", out long off));
        Assert.Equal(0, off);
    }

    [Fact]
    public void TryResolve_empty_name_returns_false()
    {
        var snap = OffsetsSnapshot.Load("{\"Pointer\": 4096}");
        Assert.False(snap.TryResolve("", out long off));
        Assert.False(snap.TryResolve(null!, out off));
    }
}
