using SupaGPO.Core.Memory;
using SupaGPO.Core.Offsets;
using SupaGPO.Core.Process;
using SupaGPO.Core.Diagnostics;

namespace SupaGPO.Core.Roblox;

/// <summary>
/// Reads character position (HumanoidRootPart) and camera look vector for navigation.
/// Uses the same memory reader infrastructure as fishing sensors.
/// </summary>
public sealed class GpoCharacterReader
{
    private readonly RobloxMemoryReader _reader;
    private long _character;
    private long _hrp;
    private long _lastUpdate;
    private long _lastDebugLog;

    public GpoCharacterReader(RobloxMemoryReader reader) => _reader = reader;

    /// <summary>Current character world position (X, Y, Z). Null if not found.</summary>
    public (double X, double Y, double Z)? Position { get; private set; }

    /// <summary>Camera LookVector (X, Z) normalized. Null if not found.</summary>
    public (double X, double Z)? CameraLook { get; private set; }

    /// <summary>True if we have valid position data.</summary>
    public bool HasData => Position is not null;

    /// <summary>
    /// Refresh position and camera. Call once per tick before navigation.
    /// Re-finds character every 2 seconds to handle respawn.
    /// </summary>
    public void Refresh()
    {
        try
        {
            long now = Environment.TickCount64;
            AppLog.Line($"[CHAR] Refresh called, Attached={_reader.IsAttached}");
            if (!_reader.IsAttached) { Position = null; CameraLook = null; return; }

            // Re-find character every 2s or if lost
            if (_character == 0 || now - _lastUpdate > 2000)
            {
                _character = FindCharacter();
                _hrp = 0;
                _lastUpdate = now;
            }

            if (_character != 0)
            {
                // Find HumanoidRootPart
                if (_hrp == 0)
                    _hrp = FindHumanoidRootPart(_character);

                if (_hrp != 0 && ReadPartPosition(_hrp, out var pos))
                    Position = pos;
            }

            // Camera look vector (X, Z)
            if (_reader.ReadCurrentCamera() is long cam && cam != 0)
            {
                if (_reader.TryReadFloat(cam + 0x150, out float lx) &&
                    _reader.TryReadFloat(cam + 0x158, out float lz))
                {
                    double len = Math.Sqrt(lx * lx + lz * lz);
                    if (len > 1e-6) CameraLook = (lx / len, lz / len);
                }
            }

            // Debug log every 5 seconds
            if (now - _lastDebugLog > 5000)
            {
                _lastDebugLog = now;
                AppLog.Line($"[CHAR] HasData={HasData} Char=0x{_character:X} HRP=0x{_hrp:X} Pos={Position} CamLook={CameraLook}");
            }
        }
        catch (Exception ex)
        {
            AppLog.Line($"[CHAR] Refresh exception: {ex.Message}");
        }
    }

    private long FindCharacter()
    {
        AppLog.Line("[CHAR] FindCharacter called");
        // Try multiple ways to get LocalPlayer
        long lp = 0;

        // Method 1: Player.LocalPlayer from offsets
        AppLog.Line("[CHAR] Trying Player.LocalPlayer offset...");
        if (_reader._offsets.TryResolve("Player.LocalPlayer", out long playerLPOff))
        {
            AppLog.Line($"[CHAR] Player.LocalPlayer offset value: 0x{playerLPOff:X} ({playerLPOff})");
        }
        else
        {
            AppLog.Line("[CHAR] Player.LocalPlayer offset NOT FOUND in offsets");
        }
        if (_reader.TryReadModulePointer("Player.LocalPlayer", out long lp1))
        {
            AppLog.Line($"[CHAR] Player.LocalPlayer pointer read: 0x{lp1:X}");
            if (RobloxMemoryReader.IsValidInstance(lp1))
                lp = lp1;
        }
        else
        {
            AppLog.Line("[CHAR] TryReadModulePointer(Player.LocalPlayer) returned false");
        }

        // Method 2: Try DataModel -> Players -> LocalPlayer
        if (lp == 0)
        {
            AppLog.Line("[CHAR] Trying DataModel -> Players -> LocalPlayer...");
            long dm = _reader.ReadDataModel();
            AppLog.Line($"[CHAR] DataModel: 0x{dm:X}");
            if (dm != 0)
            {
                // Try to find Players service by iterating DataModel children
                AppLog.Line("[CHAR] Scanning DataModel children for Players service...");
                long players = FindServiceByName(dm, "Players");
                if (players != 0)
                {
                    AppLog.Line($"[CHAR] Found Players service: 0x{players:X}");
                    // Try to get LocalPlayer from Players service
                    // Common offsets for Players.LocalPlayer
                    long[] playerOffsets = [0x10, 0x18, 0x20, 0x28, 0x30, 0x38, 0x40, 0x48, 0x50];
                    foreach (long off in playerOffsets)
                    {
                        if (_reader.TryReadPointer(players + off, out long lp2) &&
                            RobloxMemoryReader.IsValidInstance(lp2))
                        {
                            AppLog.Line($"[CHAR] Found LocalPlayer at Players+0x{off:X} = 0x{lp2:X}");
                            lp = lp2;
                            break;
                        }
                    }
                }
                else
                {
                    AppLog.Line("[CHAR] Players service not found in DataModel children");
                }
            }
            else
            {
                AppLog.Line("[CHAR] DataModel is 0");
            }
        }

        if (lp == 0)
        {
            AppLog.Line("[CHAR] Failed to find LocalPlayer");
            return 0;
        }

        // Try wider range of Character offsets
        long[] charOffsets = [0x10, 0x18, 0x20, 0x28, 0x30, 0x38, 0x40, 0x48, 0x50, 0x58, 0x60];
        foreach (long off in charOffsets)
        {
            if (_reader.TryReadPointer(lp + off, out long chr) &&
                RobloxMemoryReader.IsValidInstance(chr))
            {
                AppLog.Line($"[CHAR] Found Character at offset 0x{off:X} = 0x{chr:X}");
                return chr;
            }
        }

        AppLog.Line("[CHAR] Character not found in known offsets");
        return 0;
    }

    private long FindServiceByName(long parent, string serviceName)
    {
        // Get ChildrenStart/ChildrenEnd offsets
        if (!_reader._offsets.TryResolve("Instance.ChildrenStart", out long childrenOff) &&
            !_reader._offsets.TryResolve("Children", out childrenOff))
        {
            AppLog.Line("[CHAR] No ChildrenStart offset for service scan");
            return 0;
        }

        if (!_reader._offsets.TryResolve("Instance.Name", out long nameOff) &&
            !_reader._offsets.TryResolve("Instance.ClassName", out nameOff))
        {
            AppLog.Line("[CHAR] No Name offset for service scan");
            return 0;
        }

        long children = 0;
        if (!_reader.TryReadPointer(parent + childrenOff, out children) || children == 0)
        {
            AppLog.Line("[CHAR] Children array pointer is 0");
            return 0;
        }

        long childrenEnd = 0;
        if (!_reader._offsets.TryResolve("Instance.ChildrenEnd", out long childrenEndOff) &&
            !_reader._offsets.TryResolve("ChildrenEnd", out childrenEndOff))
            return 0;
        if (!_reader.TryReadPointer(parent + childrenEndOff, out childrenEnd) || childrenEnd == 0)
            return 0;

        long count = (childrenEnd - children) / 8;
        if (count > 200) count = 200;

        AppLog.Line($"[CHAR] Scanning {count} children for '{serviceName}' (children=0x{children:X}, childrenEnd=0x{childrenEnd:X})");

        for (int i = 0; i < count; i++)
        {
            if (!_reader.TryReadPointer(children + i * 8, out long child) || child == 0)
                continue;
            if (!RobloxMemoryReader.IsValidInstance(child)) continue;

            if (_reader.TryReadPointer(child + nameOff, out long namePtr) && namePtr != 0)
            {
                string name = _reader.ReadRobloxString(namePtr, _reader.StringLengthOffset());
                if (string.Equals(name, serviceName, StringComparison.OrdinalIgnoreCase))
                {
                    AppLog.Line($"[CHAR] Found service '{serviceName}' at index {i} = 0x{child:X}");
                    return child;
                }
                // Log first 10 children for debugging
                if (i < 10)
                    AppLog.Line($"[CHAR] Child {i}: name='{name}' addr=0x{child:X}");
            }
        }
        return 0;
    }

    private long FindHumanoidRootPart(long character)
    {
        // Character -> FindFirstChild("HumanoidRootPart")
        if (!_reader._offsets.TryResolve("Instance.ChildrenStart", out long childrenOff) &&
            !_reader._offsets.TryResolve("Children", out childrenOff))
        {
            AppLog.Line("[CHAR] No ChildrenStart offset");
            return 0;
        }

        if (!_reader._offsets.TryResolve("Instance.Name", out long nameOff) &&
            !_reader._offsets.TryResolve("Instance.ClassName", out nameOff))
        {
            AppLog.Line("[CHAR] No Name offset");
            return 0;
        }

        long children = 0;
        if (!_reader.TryReadPointer(character + childrenOff, out children) || children == 0)
        {
            AppLog.Line("[CHAR] Children array pointer is 0");
            return 0;
        }

        long childrenEnd = 0;
        if (!_reader._offsets.TryResolve("Instance.ChildrenEnd", out long childrenEndOff) &&
            !_reader._offsets.TryResolve("ChildrenEnd", out childrenEndOff))
        {
            AppLog.Line("[CHAR] No ChildrenEnd offset");
            return 0;
        }
        if (!_reader.TryReadPointer(character + childrenEndOff, out childrenEnd) || childrenEnd == 0)
        {
            AppLog.Line("[CHAR] ChildrenEnd pointer is 0");
            return 0;
        }

        // Iterate children array (Roblox uses std::vector-like: start, end)
        long count = (childrenEnd - children) / 8;
        if (count > 500) count = 500; // safety
        if (count <= 0)
        {
            AppLog.Line("[CHAR] Invalid children count");
            return 0;
        }

        AppLog.Line($"[CHAR] Scanning {count} children for HumanoidRootPart");

        for (int i = 0; i < count; i++)
        {
            if (!_reader.TryReadPointer(children + i * 8, out long child) || child == 0)
                continue;
            if (!RobloxMemoryReader.IsValidInstance(child)) continue;

            // Read name
            if (_reader.TryReadPointer(child + nameOff, out long namePtr) && namePtr != 0)
            {
                string name = _reader.ReadRobloxString(namePtr, _reader.StringLengthOffset());
                if (string.Equals(name, "HumanoidRootPart", StringComparison.OrdinalIgnoreCase))
                {
                    AppLog.Line($"[CHAR] Found HumanoidRootPart at index {i} = 0x{child:X}");
                    return child;
                }
                // Also check for common variations
                if (string.Equals(name, "HumanoidRootPart", StringComparison.OrdinalIgnoreCase) ||
                    name.Contains("RootPart", StringComparison.OrdinalIgnoreCase))
                {
                    AppLog.Line($"[CHAR] Found RootPart variant: {name} at index {i} = 0x{child:X}");
                    return child;
                }
            }
        }

        AppLog.Line("[CHAR] HumanoidRootPart not found in children");
        return 0;
    }

    private bool ReadPartPosition(long part, out (double X, double Y, double Z) pos)
    {
        pos = (0, 0, 0);
        // Part.Position usually at offset 0xC0-0xD0 from instance start
        // Also try 0x150-0x160 (some versions have it at different offsets)
        long[] posOffsets = [0xA0, 0xB0, 0xC0, 0xC8, 0xD0, 0xD8, 0xE0, 0xE8, 0xF0, 0x150, 0x158, 0x160];
        foreach (long off in posOffsets)
        {
            if (RobloxMemoryReader.IsValidInstance(part + off) &&
                _reader.TryReadFloat(part + off, out float x) &&
                _reader.TryReadFloat(part + off + 4, out float y) &&
                _reader.TryReadFloat(part + off + 8, out float z))
            {
                // Sanity check: position should be reasonable for Fish Hub area
                if (Math.Abs(x) < 10000 && Math.Abs(y) < 10000 && Math.Abs(z) < 10000)
                {
                    pos = (x, y, z);
                    return true;
                }
            }
        }
        return false;
    }
}