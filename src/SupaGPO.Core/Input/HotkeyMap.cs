namespace SupaGPO.Core.Input;

/// <summary>
/// Maps friendly hotkey names to virtual keys (dan sebaliknya). Yang didukung:
/// F1–F12, 0–9, A–Z, SPACE, ENTER, MOUSE4/MOUSE5 (tombol samping mouse).
/// </summary>
public static class HotkeyMap
{
    /// <summary>Daftar VK yang boleh dipakai saat capture keybind (diurutkan: F-key, angka, huruf, lain).</summary>
    public static readonly ushort[] CapturableKeys = BuildCapturable();

    private static ushort[] BuildCapturable()
    {
        var list = new List<ushort>();
        for (ushort vk = 0x70; vk <= 0x7B; vk++) list.Add(vk);   // F1–F12
        for (ushort vk = 0x30; vk <= 0x39; vk++) list.Add(vk);   // 0–9
        for (ushort vk = 0x41; vk <= 0x5A; vk++) list.Add(vk);   // A–Z
        list.Add(0x20);                                          // SPACE
        list.Add(0x0D);                                          // ENTER
        list.Add(0x05);                                          // XBUTTON1 (MOUSE4)
        list.Add(0x06);                                          // XBUTTON2 (MOUSE5)
        return list.ToArray();
    }

    public static ushort ToVirtualKey(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return 0x72;
        name = name.Trim().ToUpperInvariant();
        if (name.Length == 1)
        {
            char c = name[0];
            if (c >= 'A' && c <= 'Z') return (ushort)c;
            if (c >= '0' && c <= '9') return (ushort)c;
        }
        return name switch
        {
            "F1" => 0x70, "F2" => 0x71, "F3" => 0x72, "F4" => 0x73,
            "F5" => 0x74, "F6" => 0x75, "F7" => 0x76, "F8" => 0x77,
            "F9" => 0x78, "F10" => 0x79, "F11" => 0x7A, "F12" => 0x7B,
            "SPACE" => 0x20, "ENTER" => 0x0D,
            "MOUSE4" => 0x05, "MOUSE5" => 0x06,
            _ => 0x72, // default F3
        };
    }

    public static string Name(ushort vk) => vk switch
    {
        >= 0x70 and <= 0x7B => $"F{vk - 0x6F}",
        >= 0x30 and <= 0x39 => ((char)vk).ToString(),
        >= 0x41 and <= 0x5A => ((char)vk).ToString(),
        0x20 => "SPACE",
        0x0D => "ENTER",
        0x05 => "MOUSE4",
        0x06 => "MOUSE5",
        _ => "F3",
    };
}
