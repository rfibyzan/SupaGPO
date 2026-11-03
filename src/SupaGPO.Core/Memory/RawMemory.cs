using System.ComponentModel;
using System.Runtime.InteropServices;
using SupaGPO.Core.Interop;

namespace SupaGPO.Core.Memory;

/// <summary>
/// Remote process memory reader/writer. Chunked transfers, byte-count verify, 1 MB cap per call.
/// </summary>
public sealed class RawMemory
{
    private const int MaxSingleTransfer = 1024 * 1024;

    /// <summary>
    /// Test hook: bila di-set, semua Read menyala lewat delegate ini (address space
    /// palsu di memori test, tanpa proses nyata). Hanya dipakai oleh test assembly.
    /// </summary>
    internal static Func<nint, long, int, byte[]>? ReadOverride;

    public static byte[] Read(nint process, long address, int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(size);
        if (size > MaxSingleTransfer)
            throw new ArgumentOutOfRangeException(nameof(size), "transfer exceeds 1 MB cap");
        if (ReadOverride is { } fake)
            return fake(process, address, size);
        var buffer = new byte[size];
        if (!NativeMethods.ReadProcessMemory(process, (nint)address, buffer, (nint)size, out var read)
            || read.ToInt64() != size)
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return buffer;
    }

    public static void Write(nint process, long address, byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        if (data.Length == 0 || data.Length > MaxSingleTransfer)
            throw new ArgumentOutOfRangeException(nameof(data), "transfer must be 1..1 MB");
        if (!NativeMethods.WriteProcessMemory(process, (nint)address, data, (nint)data.Length, out var written)
            || written.ToInt64() != data.Length)
            throw new Win32Exception(Marshal.GetLastWin32Error());
    }

    public static T ReadStruct<T>(nint process, long address) where T : struct
    {
        int size = Marshal.SizeOf<T>();
        byte[] raw = Read(process, address, size);
        var handle = GCHandle.Alloc(raw, GCHandleType.Pinned);
        try
        {
            return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject())!;
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>Read a little-endian primitive (int/float/long/double/bool) at address.</summary>
    public static T ReadPrimitive<T>(nint process, long address) where T : unmanaged
    {
        int size = Marshal.SizeOf<T>();
        byte[] raw = Read(process, address, size);
        var handle = GCHandle.Alloc(raw, GCHandleType.Pinned);
        try
        {
            return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject())!;
        }
        finally
        {
            handle.Free();
        }
    }
}
