using System.Runtime.InteropServices;
using SupaGPO.Core.Interop;

namespace SupaGPO.Core.Input;

/// <summary>
/// Real input via P/Invoke: SendInput, mouse_event, SetCursorPos, GetAsyncKeyState.
/// Mirrors heritage input engine (MoveToWithJitter, ClickAt, SetHold/LeftDown/LeftUp, TapKey).
/// </summary>
public sealed class InputDriver : IInput
{
    private readonly Random _random = new();
    private bool _leftHeld;
    private long _leftDownSince;

    public static int ScreenWidth => InputNative.GetSystemMetrics(InputNative.SmCxScreen);
    public static int ScreenHeight => InputNative.GetSystemMetrics(InputNative.SmCyScreen);

    public bool IsProcessForeground(uint processId)
    {
        nint hwnd = InputNative.GetForegroundWindow();
        if (hwnd == 0) return false;
        InputNative.GetWindowThreadProcessId(hwnd, out uint pid);
        return pid == processId;
    }

    public (int X, int Y) GetCursor()
    {
        InputNative.GetCursorPos(out var p);
        return (p.X, p.Y);
    }

    public bool SetCursor(int x, int y) => InputNative.SetCursorPos(x, y);

    public bool TryFocusWindow(nint hwnd, uint processId)
    {
        if (hwnd == 0) return false;
        if (InputNative.IsIconic(hwnd))
            InputNative.ShowWindowAsync(hwnd, InputNative.SwRestore);
        uint targetThread = InputNative.GetWindowThreadProcessId(hwnd, out _);
        uint currentThread = InputNative.GetCurrentThreadId();
        bool attached = false;
        try
        {
            if (targetThread != 0 && targetThread != currentThread)
                attached = InputNative.AttachThreadInput(currentThread, targetThread, true);
            InputNative.BringWindowToTop(hwnd);
            InputNative.SetForegroundWindow(hwnd);
            InputNative.SetFocus(hwnd);
        }
        finally
        {
            if (attached) InputNative.AttachThreadInput(currentThread, targetThread, false);
        }
        return IsProcessForeground(processId);
    }

    public bool SendKey(ushort virtualKey, bool down, bool extended = false)
    {
        uint flags = (down ? 0u : InputNative.KeyEventKeyUp)
            | (extended ? InputNative.KeyEventExtendedKey : 0u);
        ushort scan = (ushort)InputNative.MapVirtualKeyW(virtualKey, InputNative.MapVkToVsc);

        if (scan != 0)
        {
            var scanInput = new InputNative.Input
            {
                Type = InputNative.InputKeyboard,
                Data = new InputNative.InputUnion
                {
                    Keyboard = new InputNative.KeyboardInput
                    {
                        ScanCode = scan,
                        Flags = flags | InputNative.KeyEventScanCode
                    }
                }
            };
            if (InputNative.SendInput(1, ref scanInput, Marshal.SizeOf<InputNative.Input>()) == 1)
                return true;
        }

        var vkInput = new InputNative.Input
        {
            Type = InputNative.InputKeyboard,
            Data = new InputNative.InputUnion
            {
                Keyboard = new InputNative.KeyboardInput
                {
                    VirtualKey = virtualKey,
                    Flags = flags
                }
            }
        };
        if (InputNative.SendInput(1, ref vkInput, Marshal.SizeOf<InputNative.Input>()) == 1)
            return true;

        InputNative.keybd_event((byte)virtualKey, 0, flags, 0);
        return true;
    }

    public void TapKey(ushort virtualKey, bool extended = false)
    {
        SendKey(virtualKey, true, extended);
        System.Threading.Thread.Sleep(35);
        SendKey(virtualKey, false, extended);
        System.Threading.Thread.Sleep(12);
    }

    public void LeftDown()
    {
        InputNative.mouse_event(InputNative.MouseLeftDown, 0, 0, 0, 0);
        _leftHeld = true;
        _leftDownSince = Environment.TickCount64;
    }

    public void LeftUp()
    {
        InputNative.mouse_event(InputNative.MouseLeftUp, 0, 0, 0, 0);
        _leftHeld = false;
        _leftDownSince = 0;
    }

    public void RightDown() => InputNative.mouse_event(InputNative.MouseRightDown, 0, 0, 0, 0);

    public void RightUp() => InputNative.mouse_event(InputNative.MouseRightUp, 0, 0, 0, 0);

    public void ReleaseAllButtons()
    {
        LeftUp();
        RightUp();
        ReleaseAllKeys();
    }

    public void ClickAt(int x, int y, int holdMs = 30)
    {
        MoveTo(x, y);
        LeftDown();
        System.Threading.Thread.Sleep(Math.Max(1, holdMs));
        LeftUp();
    }

    public bool ClickAtClientPoint(nint hwnd, int clientX, int clientY)
    {
        if (hwnd == 0) return false;
        var pt = new InputNative.Point { X = clientX, Y = clientY };
        if (!InputNative.ClientToScreen(hwnd, ref pt)) return false;
        ClickAt(pt.X, pt.Y);
        return true;
    }

    public void MoveTo(int x, int y, int jitterPx = 0)
    {
        if (jitterPx > 0)
        {
            x += _random.Next(-jitterPx, jitterPx + 1);
            y += _random.Next(-jitterPx, jitterPx + 1);
        }
        InputNative.SetCursorPos(x, y);
    }

    public bool IsPressed(ushort virtualKey) =>
        (InputNative.GetAsyncKeyState(virtualKey) & 0x8000) != 0;

    public bool LeftHeld => _leftHeld;
    public long LeftDownSince => _leftDownSince;

    // === Key hold for navigation ===
    private readonly HashSet<ushort> _heldKeys = new();

    public void KeyDown(ushort virtualKey, bool extended = false)
    {
        if (_heldKeys.Add(virtualKey))
            SendKey(virtualKey, true, extended);
    }

    public void KeyUp(ushort virtualKey, bool extended = false)
    {
        if (_heldKeys.Remove(virtualKey))
            SendKey(virtualKey, false, extended);
    }

    public void ReleaseAllKeys()
    {
        foreach (ushort vk in _heldKeys.ToArray())
            KeyUp(vk);
    }

    public bool IsKeyHeld(ushort virtualKey) => _heldKeys.Contains(virtualKey);
}
