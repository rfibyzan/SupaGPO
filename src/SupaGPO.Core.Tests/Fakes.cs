using SupaGPO.Core.Fishing;
using SupaGPO.Core.Input;

namespace SupaGPO.Core.Tests;

public sealed class FakeInput : IInput
{
    public bool LeftHeld { get; private set; }
    public long LeftDownSince { get; private set; }
    public readonly HashSet<ushort> HeldKeys = new();
    public readonly List<string> Calls = new();

    public bool IsProcessForeground(uint pid) => true;
    public void LeftDown() { Calls.Add("LeftDown"); LeftHeld = true; LeftDownSince = Environment.TickCount64; }
    public void LeftUp() { Calls.Add("LeftUp"); LeftHeld = false; LeftDownSince = 0; }
    public void RightDown() => Calls.Add("RightDown");
    public void RightUp() => Calls.Add("RightUp");
    public void ReleaseAllButtons() { Calls.Add("ReleaseAll"); LeftHeld = false; LeftDownSince = 0; ReleaseAllKeys(); }
    public void TapKey(ushort vk, bool extended = false) => Calls.Add($"TapKey{vk}");
    public bool ClickAtClientPoint(nint hwnd, int x, int y) { Calls.Add("ClickClient"); return true; }

    public void KeyDown(ushort virtualKey, bool extended = false) { HeldKeys.Add(virtualKey); Calls.Add($"KeyDown{virtualKey}"); }
    public void KeyUp(ushort virtualKey, bool extended = false) { HeldKeys.Remove(virtualKey); Calls.Add($"KeyUp{virtualKey}"); }
    public void ReleaseAllKeys() { foreach (var vk in HeldKeys.ToArray()) KeyUp(vk); }
    public bool IsKeyHeld(ushort virtualKey) => HeldKeys.Contains(virtualKey);
}

public sealed class FakeSensors : IFishingSensors
{
    public bool ReelActive { get; set; }
    public int BaitCount { get; set; }
    public string? PreferredBait { get; set; }
    public string[] Fruits { get; set; } = Array.Empty<string>();
    public string[] SpawnNotices { get; set; } = Array.Empty<string>();
    public bool AutoOpenChests { get; set; }
    public bool AutoStoreFruits { get; set; }
    public string? LastChestRemoved { get; set; }
    public (float X, float Y, float Z)? CameraPosition { get; set; }
}
