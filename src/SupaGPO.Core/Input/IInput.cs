namespace SupaGPO.Core.Input;

/// <summary>
/// Input contract used by FishingLoop / CastFsm / ReelHandler. Real implementation = InputDriver
/// (P/Invoke); tests use a fake.
/// </summary>
public interface IInput
{
    bool IsProcessForeground(uint processId);
    void LeftDown();
    void LeftUp();
    void RightDown();
    void RightUp();
    void ReleaseAllButtons();
    void TapKey(ushort virtualKey, bool extended = false);
    bool ClickAtClientPoint(nint hwnd, int clientX, int clientY);
    bool LeftHeld { get; }
    long LeftDownSince { get; }

    // Key hold for navigation
    void KeyDown(ushort virtualKey, bool extended = false);
    void KeyUp(ushort virtualKey, bool extended = false);
    void ReleaseAllKeys();
    bool IsKeyHeld(ushort virtualKey);
}
