namespace SupaGPO.Core.Fishing;

/// <summary>
/// Known Common Fish Bait shop positions in Fish Hub (from RobloxInstanceExplorer snapshot).
/// All Y ≈ 3.372, MaxActivationDistance = 5.044.
/// </summary>
public static class BaitShopPositions
{
    /// <summary>All known positions (X, Z). Y is always ~3.372.</summary>
    public static readonly (double X, double Z)[] All =
    [
        (78.751, -1417.262),  // #1 closest to center
        (78.617, -1446.437),  // #2
        (80.911, -1431.758),  // #4
        (71.992, -1404.046),  // #15
        (71.915, -1459.589),  // #3
        (61.612, -1393.624),  // #12
        (61.501, -1469.948),  // #8
        (48.413, -1386.905),  // #14
        (48.302, -1476.714),  // #5
        (33.837, -1384.696),  // #11
        (33.843, -1478.977),  // #7
        (19.237, -1386.878),  // #10
        (19.293, -1476.750),  // #6
        (6.006, -1393.572),   // #13
        (6.032, -1469.929),   // #9
        (-4.337, -1459.592),  // #17
        (-4.433, -1404.010),  // #20
        (-11.143, -1446.458), // #16
        (-11.206, -1417.194), // #18
        (-13.319, -1431.899)  // #19
    ];

    /// <summary>Find the nearest bait shop to the given position.</summary>
    public static (double X, double Z) Nearest(double x, double z)
    {
        double bestDist = double.MaxValue;
        (double X, double Z) best = All[0];
        foreach (var p in All)
        {
            double dx = p.X - x, dz = p.Z - z;
            double d = dx * dx + dz * dz;
            if (d < bestDist) { bestDist = d; best = p; }
        }
        return best;
    }
}