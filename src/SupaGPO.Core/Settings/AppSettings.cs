namespace SupaGPO.Core.Settings;

/// <summary>
/// Fishing automation GPO (rekonstruksi Grand Piece.exe): cast = klik singkat (CastClickMs),
/// tunggu bite hingga BiteTimeoutSec, jeda AfterCatchMs setelah minigame selesai.
/// AutoRefillBait/RefillAmount = isi ulang Common bait; AutoBuyLegBait = sub-tab Trading Hub (segera).
/// AutoWalkToBait = jalan ke penjual bait jika prompt tidak terjangkau; WalkTimeoutSec = batas aman.
/// </summary>
public sealed record FishingSettings(
    bool Enabled = false,
    int RodSlot = 1,
    int CastClickMs = 50,
    int BiteTimeoutSec = 60,
    bool AutoRefillBait = true,
    int RefillAmount = 5,
    int RefillEvery = 10,
    int AfterCatchMs = 700,
    bool AutoBuyLegBait = false,
    bool EquipOnStart = false,
    bool AutoWalkToBait = false,
    int WalkTimeoutSec = 20);

/// <summary>Loot automation: AutoOpenChests + AutoStoreChestFruits (from Grand Piece.exe strings).</summary>
public sealed record LootSettings(bool Enabled = false, bool AutoOpenChests = true, bool AutoStoreFruits = true);

/// <summary>General: macro hotkey (Start/Stop, default F3 seperti Grand Piece.exe), overlay visibility.</summary>
public sealed record GeneralSettings(string MacroHotkey = "F3", bool ShowOverlay = true);

/// <summary>Discord webhook events.</summary>
public sealed record WebhookSettings(
    string WebhookUrl = "",
    bool FishingStarted = true,
    bool FishingStopped = true,
    bool CatchNotify = true);

/// <summary>Offsets source URL.</summary>
public sealed record OffsetsSettings(string ProviderUrl = OffsetsSettings.DefaultProviderUrl)
{
    // Default mengikuti SupaZero: imtheo (format dumper langsung, tanpa wrapper).
    // GitHub Astro (wrapper RbxDumperV2) tetap didukung parser sebagai legacy.
    public const string DefaultProviderUrl = "https://offsets.imtheo.lol/offsets.json";
    public const string LegacyProviderUrl = "https://raw.githubusercontent.com/5ran/astro-launcher-releases2/main/offsets/offsets.json";
}

/// <summary>Akar preferensi. Immutable sub-records replaced whole via setter, firing Changed.</summary>
public sealed class AppSettings
{
    public event Action<string>? Changed;

    private T Set<T>(ref T field, T value, string name) where T : class
    {
        value ??= field;
        if (ReferenceEquals(field, value)) return field;
        field = value;
        Changed?.Invoke(name);
        return field;
    }

    /// <summary>Versi migrasi preferensi (0 = file lama belum dimigrasi;1 = sudah).</summary>
    private int _prefsVersion;
    public int PrefsVersion
    {
        get => _prefsVersion;
        set { if (_prefsVersion == value) return; _prefsVersion = value; Changed?.Invoke(nameof(PrefsVersion)); }
    }

    private FishingSettings _fishing = new();
    public FishingSettings Fishing { get => _fishing; set => Set(ref _fishing, value, nameof(Fishing)); }

    private LootSettings _loot = new();
    public LootSettings Loot { get => _loot; set => Set(ref _loot, value, nameof(Loot)); }

    private GeneralSettings _general = new();
    public GeneralSettings General { get => _general; set => Set(ref _general, value, nameof(General)); }

    private WebhookSettings _webhook = new();
    public WebhookSettings Webhook { get => _webhook; set => Set(ref _webhook, value, nameof(Webhook)); }

    private OffsetsSettings _offsets = new();
    public OffsetsSettings Offsets { get => _offsets; set => Set(ref _offsets, value, nameof(Offsets)); }
}
