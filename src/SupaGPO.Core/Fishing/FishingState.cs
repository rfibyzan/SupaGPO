namespace SupaGPO.Core.Fishing;

/// <summary>Snapshot of Grand Piece Online fishing state read from Roblox memory.</summary>
public sealed record FishingState
{
    public bool ReelActive { get; init; }
    public int BaitCount { get; init; }
    public string? PreferredBait { get; init; }
    public string[] Fruits { get; init; } = Array.Empty<string>();
    public string[] SpawnNotices { get; init; } = Array.Empty<string>();
    public bool AutoOpenChests { get; init; }
    public bool AutoStoreFruits { get; init; }
    public string? LastChestRemoved { get; init; }
    public (float X, float Y, float Z)? CameraPosition { get; init; }

    public static FishingState Empty { get; } = new() { ReelActive = false };
}

public enum CatchOutcome { Unknown, Caught, Lost }

public sealed record FishingStatus(string Category, string Text, bool FlagA = false, double Value = -1);

public static class FishingTexts
{
    public const string CategoryPassive = "Passive";
    public const string CategoryCasting = "Casting";
    public const string CategoryRecover = "Recover";
    public const string CategoryLoot = "Loot";
    public const string CategoryWebhook = "Webhook";

    public const string WaitingForRobloxFocus = "Waiting for Roblox focus.";
    public const string RobloxLostFocus = "Roblox lost focus.";
    public const string WaitingForManualCast = "Waiting for manual cast.";
    public const string StartingNormalCasting = "Starting normal casting.";
    public const string NormalCasting = "Normal casting.";
    public const string ReelDetected = "Reel detected.";
    public const string ReelDetectedManualReel = "Reel detected — manual reel.";
    public const string WaitingBeforeRecast = "Waiting before recast.";
    public const string RuntimeSignalStalled = "Runtime signal stalled; recovery reset applied.";
    public const string CatchCompleteWaiting = "Catch complete — waiting.";
    public const string LootChestsOpened = "Chests opened.";
    public const string LootFruitsStored = "Fruits stored.";
    public const string LoggerMousePulse = "MousePulse";
    public const string LoggerRecover = "Recover";
    public const string LoggerLoot = "Loot";
}
