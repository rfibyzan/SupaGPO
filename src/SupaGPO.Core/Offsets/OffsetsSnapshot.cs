using System.Text.Json;
using System.Text.Json.Serialization;

namespace SupaGPO.Core.Offsets;

/// <summary>
/// Loaded offsets for the live Roblox build. Groups: DataModel, Workspace, PlayerGui, Fishing,
/// Loot, Camera. Missing required key => <see cref="MissingOffsetException"/>.
/// </summary>
public sealed class MissingOffsetException(string key) : Exception($"Missing offset: {key}");

public sealed class OffsetsSnapshot
{
    private static readonly JsonSerializerOptions SaveOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Dictionary<string, object> _values = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Aliases untuk kunci warisan SupaGPO → skema RbxDumperV2 (nested groups).</summary>
    private static readonly IReadOnlyDictionary<string, string> KeyAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Children"] = "Instance.ChildrenStart",
            ["ChildrenEnd"] = "Instance.ChildrenEnd",
            ["ClassDescriptor"] = "Instance.ClassDescriptor",
            ["ClassDescriptorToClassName"] = "Instance.ClassName",
            ["StringLength"] = "Misc.StringLength",
        };

    public int Count => _values.Count;

    /// <summary>Versi Roblox dari metadata provider (wrapper "Roblox Version"); null jika tidak ada.</summary>
    public string? RobloxVersion { get; private set; }

    public bool HasVersionInfo => !string.IsNullOrWhiteSpace(RobloxVersion);

    /// <summary>
    /// true bila offsets cocok untuk versi Roblox live. Tanpa info versi di offsets
    /// dianggap cocok (format flat lama) — mirip perilaku tolerant Grand Piece.exe.
    /// </summary>
    public bool MatchesRobloxVersion(string? liveVersion) =>
        !HasVersionInfo ||
        string.Equals(RobloxVersion, liveVersion, StringComparison.OrdinalIgnoreCase);

    public static OffsetsSnapshot Load(string json)
    {
        var doc = JsonDocument.Parse(json);
        var snap = new OffsetsSnapshot();
        foreach (var prop in doc.RootElement.EnumerateObject())
            snap._values[prop.Name] = ConvertToken(prop.Value);

        // Format wrapper provider (RbxDumperV2): offset asli berupa JSON string
        // ter-escape di properti "Offsets" + metadata "Roblox Version" di root.
        if (snap._values.TryGetValue("Roblox Version", out var rv))
            snap.RobloxVersion = rv as string;

        if (snap._values.TryGetValue("Offsets", out var nested) && nested is string nestedJson)
            snap.MergeNestedJson(nestedJson);
        return snap;
    }

    /// <summary>Parse JSON nested (skema RbxDumperV2) dan flatten jadi kunci "Group.Name".</summary>
    private void MergeNestedJson(string nestedJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(nestedJson);
            FlattenInto(doc.RootElement, prefix: null);
        }
        catch
        {
            // Nested rusak: biarkan kunci root apa adanya (tolerant, tidak throw).
        }
    }

    private void FlattenInto(JsonElement el, string? prefix)
    {
        if (el.ValueKind != JsonValueKind.Object) return;
        foreach (var prop in el.EnumerateObject())
        {
            var key = prefix == null ? prop.Name : $"{prefix}.{prop.Name}";
            if (prop.Value.ValueKind == JsonValueKind.Object)
                FlattenInto(prop.Value, key);
            else
                _values[key] = ConvertToken(prop.Value);
        }
    }

    /// <summary>Load offsets from a file. Returns null if the file is missing or unreadable.</summary>
    public static OffsetsSnapshot? LoadFromFile(string path)
    {
        try
        {
            if (!File.Exists(path)) return null;
            var json = File.ReadAllText(path);
            return Load(json);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Serialize this snapshot to JSON text.</summary>
    public string Serialize() => JsonSerializer.Serialize(_values, SaveOptions);

    /// <summary>Atomically write this snapshot to a file.</summary>
    public void SaveToFile(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string json = Serialize();
        string temp = path + ".tmp";
        File.WriteAllText(temp, json);
        File.Move(temp, path, overwrite: true);
    }

    private static object ConvertToken(JsonElement el)
    {
        return el.ValueKind switch
        {
            JsonValueKind.Number => el.TryGetInt64(out var l) ? (object)l : el.GetDouble(),
            JsonValueKind.String => el.GetString()!,
            JsonValueKind.True or JsonValueKind.False => el.GetBoolean(),
            _ => el.GetRawText()
        };
    }

    public bool TryGet(string key, out object? value) => _values.TryGetValue(key, out value);

    public long GetLong(string key)
    {
        if (!_values.TryGetValue(key, out var v))
            throw new MissingOffsetException(key);
        return Convert.ToInt64(v);
    }

    public int GetInt(string key)
    {
        if (!_values.TryGetValue(key, out var v))
            throw new MissingOffsetException(key);
        return Convert.ToInt32(v);
    }

    public string GetString(string key)
    {
        if (!_values.TryGetValue(key, out var v))
            throw new MissingOffsetException(key);
        return Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }

    public bool Contains(string key) => _values.ContainsKey(key);

    /// <summary>
    /// Resolve an offset key. Urutan: exact → alias warisan (mis. "Children" →
    /// "Instance.ChildrenStart") → suffix dot-notation. Tolerant: tidak pernah throw.
    /// </summary>
    public bool TryResolve(string name, out long offset)
    {
        offset = 0;
        if (string.IsNullOrWhiteSpace(name))
            return false;

        // Exact match first.
        if (_values.TryGetValue(name, out var v) && ConvertToLong(v, out long exact))
        {
            offset = exact;
            return true;
        }

        // Alias warisan SupaGPO ke skema RbxDumperV2.
        if (KeyAliases.TryGetValue(name, out var alias) &&
            !string.Equals(alias, name, StringComparison.OrdinalIgnoreCase) &&
            _values.TryGetValue(alias, out v) && ConvertToLong(v, out long aliased))
        {
            offset = aliased;
            return true;
        }

        // Dot-notation fallback: try each suffix after a dot.
        int dot = name.IndexOf('.');
        while (dot >= 0 && dot < name.Length - 1)
        {
            var suffix = name[(dot + 1)..];
            if (_values.TryGetValue(suffix, out v) && ConvertToLong(v, out long suffixOffset))
            {
                offset = suffixOffset;
                return true;
            }
            dot = name.IndexOf('.', dot + 1);
        }
        return false;
    }

    private static bool ConvertToLong(object? value, out long result)
    {
        result = 0;
        if (value == null) return false;
        try
        {
            result = Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture);
            return true;
        }
        catch { return false; }
    }
}
