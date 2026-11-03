using SupaGPO.Core.Interop;

namespace SupaGPO.Core.Input;

/// <summary>
/// Hotkey checker: polls GetAsyncKeyState for the configured macro hotkey (Start/Stop toggle).
/// The Presentation layer calls IsHotkeyPressed on a timer. Avoids a fragile global hook.
/// </summary>
public sealed class HotkeyService
{
    public HotkeyService(string hotkeyName)
    {
        VirtualKey = HotkeyMap.ToVirtualKey(hotkeyName);
        HotkeyName = hotkeyName;
    }

    public ushort VirtualKey { get; }
    public string HotkeyName { get; }

    public bool IsHotkeyPressed() => IsVkDown(VirtualKey);

    /// <summary>True bila tombol virtual key sedang ditekan (GetAsyncKeyState).</summary>
    public static bool IsVkDown(ushort vk) =>
        vk != 0 && (InputNative.GetAsyncKeyState(vk) & 0x8000) != 0;
}
