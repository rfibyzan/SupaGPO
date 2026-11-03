namespace SupaGPO.Core.Input;

/// <summary>
/// Decorator around IInput that records LMB hold state and down-time, for watchdogs.
/// </summary>
public sealed class TrackingInput : IInput
{
    private readonly IInput _inner;
    private bool _leftHeld;
    private long _leftDownSince;

    public TrackingInput(IInput inner) => _inner = inner;

    public bool IsProcessForeground(uint pid) => _inner.IsProcessForeground(pid);
    public void LeftDown() { _inner.LeftDown(); _leftHeld = true; _leftDownSince = Environment.TickCount64; }
    public void LeftUp() { _inner.LeftUp(); _leftHeld = false; _leftDownSince = 0; }
    public void RightDown() => _inner.RightDown();
    public void RightUp() => _inner.RightUp();
    public void ReleaseAllButtons() { _inner.ReleaseAllButtons(); _leftHeld = false; _leftDownSince = 0; }
    public void TapKey(ushort vk, bool extended = false) => _inner.TapKey(vk, extended);
    public bool ClickAtClientPoint(nint hwnd, int x, int y) => _inner.ClickAtClientPoint(hwnd, x, y);
    public bool LeftHeld => _leftHeld;
    public long LeftDownSince => _leftDownSince;

    public void KeyDown(ushort virtualKey, bool extended = false) => _inner.KeyDown(virtualKey, extended);
    public void KeyUp(ushort virtualKey, bool extended = false) => _inner.KeyUp(virtualKey, extended);
    public void ReleaseAllKeys() => _inner.ReleaseAllKeys();
    public bool IsKeyHeld(ushort virtualKey) => _inner.IsKeyHeld(virtualKey);
}
