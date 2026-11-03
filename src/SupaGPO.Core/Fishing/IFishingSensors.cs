namespace SupaGPO.Core.Fishing;

/// <summary>
/// Sensor contract for fishing state. Production = RobloxFishingSensors (reads Roblox memory);
/// tests use an in-memory fake.
/// </summary>
public interface IFishingSensors
{
    bool ReelActive { get; }
    int BaitCount { get; }
    string? PreferredBait { get; }
    string[] Fruits { get; }
    string[] SpawnNotices { get; }
    bool AutoOpenChests { get; }
    bool AutoStoreFruits { get; }
    string? LastChestRemoved { get; }
    (float X, float Y, float Z)? CameraPosition { get; }
}
