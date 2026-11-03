namespace SupaGPO.Core.Fishing;

/// <summary>Notice terstruktur hasil parse feed teks: jenis + lokasi (bila ada) + teks mentah.</summary>
public sealed record SpawnNotice(string Kind, string? Location, string Raw)
{
    /// <summary>Bentuk kanonik untuk konsumen (UI/webhook): "Fruit spawned at Tow Town" / teks mentah.</summary>
    public string Canonical => Kind switch
    {
        "fruit-spawn" => Location is null ? "Fruit spawned" : $"Fruit spawned at {Location}",
        _ => Raw,
    };
}

/// <summary>
/// Parser feed teks Roblox untuk spawn notice — meniru asli (REA: template frozen
/// string "A fruit has spawned at …", VA 0x141CDC6F2). Pure & tolerant: baris yang
/// tidak cocok template dilewatkan apa adanya; tidak pernah throw.
/// </summary>
public static class SpawnNoticeParser
{
    // Template terverifikasi dari binary asli (0x1C790FE); kasus tanpa lokasi ditoleransi.
    private const string FruitSpawnAtPrefix = "a fruit has spawned at ";
    private const string FruitSpawnNoLoc = "a fruit has spawned";

    private static readonly char[] TrimPunctuation = { '.', '!', '…', ' ' };

    public static SpawnNotice ParseLine(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return new SpawnNotice("empty", null, string.Empty);

        var text = line.Trim();
        var lower = text.ToLowerInvariant();

        if (lower.StartsWith(FruitSpawnAtPrefix, StringComparison.Ordinal))
        {
            var location = text[FruitSpawnAtPrefix.Length..].Trim().TrimEnd(TrimPunctuation).Trim();
            return new SpawnNotice("fruit-spawn", location.Length == 0 ? null : location, text);
        }

        if (lower.TrimEnd('.', '!', '…', ' ').Equals(FruitSpawnNoLoc, StringComparison.Ordinal))
            return new SpawnNotice("fruit-spawn", null, text);

        // Baris lain (chat, system notice lain) — lewatkan apa adanya.
        return new SpawnNotice("raw", null, text);
    }

    /// <summary>
    /// Parse batch teks feed: pisah per baris ('|' atau newline, menyerupai buffer
    /// legacy) lalu normalisasi tiap baris ke bentuk kanonik.
    /// </summary>
    public static string[] ParseBatch(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();
        return text
            .Split(new[] { '|', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(l => ParseLine(l).Canonical)
            .Where(c => c.Length != 0)
            .ToArray();
    }
}
