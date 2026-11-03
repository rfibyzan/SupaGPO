namespace SupaGPO.Core.Loot;

/// <summary>
/// Loot automation: AutoOpenChests + AutoStoreChestFruits (from Grand Piece.exe strings
/// get_AutoOpenChests / get_AutoStoreChestFruits / LastChestRemoved).
/// </summary>
public sealed class LootController
{
    public bool Enabled { get; set; }
    public bool AutoOpenChests { get; set; } = true;
    public bool AutoStoreFruits { get; set; } = true;

    private bool _chestsLogged;
    private bool _fruitsLogged;

    public Fishing.FishingStatus? Tick(Fishing.FishingState state)
    {
        if (!Enabled) return null;

        if (state.AutoOpenChests && !_chestsLogged)
        {
            _chestsLogged = true;
            _fruitsLogged = false;
            return new Fishing.FishingStatus(Fishing.FishingTexts.CategoryLoot,
                Fishing.FishingTexts.LootChestsOpened, FlagA: true);
        }
        if (!state.AutoOpenChests) _chestsLogged = false;

        if (state.AutoStoreFruits && state.Fruits.Length > 0 && !_fruitsLogged)
        {
            _fruitsLogged = true;
            return new Fishing.FishingStatus(Fishing.FishingTexts.CategoryLoot,
                Fishing.FishingTexts.LootFruitsStored, FlagA: true);
        }
        if (state.Fruits.Length == 0) _fruitsLogged = false;

        return null;
    }
}
