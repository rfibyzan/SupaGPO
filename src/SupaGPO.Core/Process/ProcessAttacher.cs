using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SupaGPO.Core.Interop;

namespace SupaGPO.Core.Process;

/// <summary>
/// Attaches to RobloxPlayerBeta by process name (newest first), opens a read handle, and keeps it.
/// "Roblox not running" is a normal state — never throws.
/// Mirrors heritage RobloxProcessLocator: OrderByDescending(StartTime).First.
/// </summary>
public sealed class ProcessAttacher : IDisposable
{
    private nint _handle;
    private int _pid;

    public bool IsAttached => _handle != 0;
    public int ProcessId => _pid;
    internal nint Handle => _handle;

    public bool TryAttach(string processName, out string status)
    {
        var newest = PickNewest(System.Diagnostics.Process.GetProcessesByName(processName));
        if (newest is null)
        {
            status = "Roblox is not running.";
            return false;
        }
        if (_handle != 0 && _pid == newest.Id)
        {
            status = "Already attached.";
            return true;
        }
        Detach();
        nint handle = NativeMethods.OpenProcess(NativeMethods.AttachAccess, false, (uint)newest.Id);
        if (handle == 0)
        {
            status = "Could not open Roblox for memory reads: "
                + new Win32Exception(Marshal.GetLastWin32Error()).Message;
            return false;
        }
        _handle = handle;
        _pid = newest.Id;
        status = "Attached.";
        return true;
    }

    public static System.Diagnostics.Process? PickNewest(IEnumerable<System.Diagnostics.Process> processes) =>
        processes.OrderByDescending(SafeStartTime).FirstOrDefault();

    private static DateTime SafeStartTime(System.Diagnostics.Process p)
    {
        try { return p.StartTime; }
        catch { return DateTime.MinValue; }
    }

    public void Detach()
    {
        if (_handle != 0)
        {
            NativeMethods.CloseHandle(_handle);
            _handle = 0;
            _pid = 0;
        }
    }

    public void Dispose() => Detach();
}
