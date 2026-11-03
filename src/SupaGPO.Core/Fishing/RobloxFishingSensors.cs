using SupaGPO.Core.Roblox;
using SupaGPO.Core.Settings;
using SupaGPO.Core.Visual;

namespace SupaGPO.Core.Fishing;

/// <summary>
/// Sensor memori Grand Piece Online, meniru desain asli (REA): jalur tunggal =
/// flat read di module base + offset key dari offsets.json (offsets-driven), ditambah
/// kamera via rantai engine (DataModel → Workspace → CurrentCamera). Deteksi reel visual
/// (probe piksel) tetap didukung untuk ReelActive. Kunci GPO-spesifik (Fishing.*/Loot.*)
/// tidak ada di dump publik → inert, degradasi graceful tanpa throw.
/// Loot flags (AutoOpenChests/AutoStoreFruits) = settings milik macro sendiri (evidence REA #10),
/// bukan pembacaan memori.
/// </summary>
public sealed class RobloxFishingSensors : IFishingSensors
{
    private readonly RobloxMemoryReader _reader;
    private readonly VisualReelProbe? _visualReel;
    private readonly Func<LootSettings> _loot;

    public RobloxFishingSensors(RobloxMemoryReader reader, VisualReelProbe? visualReel = null,
        Func<LootSettings>? loot = null)
    {
        _reader = reader;
        _visualReel = visualReel;
        _loot = loot ?? (() => new LootSettings());
    }

    public bool ReelActive
    {
        get
        {
            if (_visualReel?.Probe() == true) return true;
            return _reader.TryReadModuleByte("Fishing.ReelFlag", out byte v) && v != 0;
        }
    }

    public int BaitCount =>
        _reader.TryReadModuleInt32("Fishing.BaitCount", out int v) ? v : 0;

    public string? PreferredBait
    {
        get
        {
            if (!_reader.TryReadModulePointer("Fishing.PreferredBait", out long ptr) || ptr == 0)
                return null;
            string s = _reader.ReadRobloxString(ptr, _reader.StringLengthOffset());
            return s.Length != 0 ? s : null;
        }
    }

    public string[] Fruits
    {
        get
        {
            if (!_reader.TryReadModulePointer("Fishing.FruitsList", out long list) || list == 0)
                return Array.Empty<string>();
            if (!_reader.TryReadInt32(list, out int count) || count <= 0 || count > 100)
                return Array.Empty<string>();
            var result = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                if (!_reader.TryReadPointer(list + 4 + (long)i * 8, out long sPtr) || sPtr == 0) break;
                string s = _reader.ReadRobloxString(sPtr, _reader.StringLengthOffset());
                if (s.Length == 0) break;
                result.Add(s);
            }
            return result.ToArray();
        }
    }

    public string[] SpawnNotices
    {
        get
        {
            if (!_reader.TryReadModulePointer("Fishing.SpawnNotices", out long buf) || buf == 0)
                return Array.Empty<string>();
            if (!_reader.TryReadBytes(buf, 1024, out byte[] data)) return Array.Empty<string>();
            var text = System.Text.Encoding.Unicode.GetString(data);
            int end = text.IndexOf('\0');
            if (end >= 0) text = text[..end];
            return SpawnNoticeParser.ParseBatch(text);
        }
    }

    public bool AutoOpenChests => _loot().AutoOpenChests;
    public bool AutoStoreFruits => _loot().AutoStoreFruits;

    public string? LastChestRemoved
    {
        get
        {
            if (!_reader.TryReadModulePointer("Loot.LastChestRemoved", out long ptr) || ptr == 0)
                return null;
            string s = _reader.ReadRobloxString(ptr, _reader.StringLengthOffset());
            return s.Length != 0 ? s : null;
        }
    }

    // Kamera live lewat rantai engine dump nyata (offset per-instance, bukan static file offset).
    public (float X, float Y, float Z)? CameraPosition
    {
        get
        {
            long cam = _reader.ReadCurrentCamera();
            if (cam == 0 || !_reader._offsets.TryResolve("Camera.Position", out long off)) return null;
            if (!_reader.TryReadFloat(cam + off, out float x) ||
                !_reader.TryReadFloat(cam + off + 4, out float y) ||
                !_reader.TryReadFloat(cam + off + 8, out float z))
                return null;
            return (x, y, z);
        }
    }
}
