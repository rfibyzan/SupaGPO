using System.Runtime.InteropServices;
using System.Text;
using SupaGPO.Core.Memory;
using SupaGPO.Core.Offsets;
using SupaGPO.Core.Process;

namespace SupaGPO.Core.Roblox;

/// <summary>
/// Pembaca memori Roblox untuk Grand Piece Online (eksternal, tidak injeksi).
/// Jalur (meniru desain Grand Piece.exe): flat reads di module base + offset key dari
/// offsets.json, plus kamera via rantai engine (DataModel → Workspace → CurrentCamera).
/// Semua baca toleran: offset hilang / Roblox absen = keadaan normal (hasil kosong/0), tanpa throw.
/// </summary>
public sealed class RobloxMemoryReader : IDisposable
{
    private readonly ProcessAttacher _attacher;
    private readonly RawMemory _memory = new();
    internal OffsetsSnapshot _offsets;

    /// <summary>Swap snapshot offsets saat reload/download — sensor membaca lewat reader.</summary>
    public OffsetsSnapshot OffsetsSnapshot
    {
        get => _offsets;
        set => _offsets = value ?? _offsets;
    }

    private nint _process;
    private long _exeBase;
    private bool _attached;

    public RobloxMemoryReader(ProcessAttacher attacher, OffsetsSnapshot offsets)
    {
        _attacher = attacher;
        _offsets = offsets;
    }

    public bool IsAttached => _attached && _process != 0;
    public int ProcessId => _attacher.ProcessId;
    internal nint Process => _process;
    public long ModuleBase => _exeBase;

    /// <summary>Attach ke RobloxPlayerBeta dan risolusi base address modul.</summary>
    public bool TryAttach(out string status)
    {
        if (!_attacher.TryAttach("RobloxPlayerBeta", out status))
        {
            _attached = false;
            _process = 0;
            _exeBase = 0;
            return false;
        }
        _process = _attacher.Handle;
        _exeBase = MainModuleBase(_attacher.ProcessId);
        _attached = _process != 0 && _exeBase != 0;
        if (!_attached)
        {
            status = "Attached to process but could not read module base.";
            return false;
        }
        status = "Attached.";
        return true;
    }

    public void Detach()
    {
        _attached = false;
        _process = 0;
        _exeBase = 0;
        _attacher.Detach();
    }

    // ===== validasi alamat instance =====

    /// <summary>Alamat instance valid: 0xFFFF < addr < 0x800000000000.</summary>
    public static bool IsValidInstance(long address) =>
        (ulong)address > 0xFFFF && (ulong)address < 0x800000000000;

    /// <summary>Baca pointer 8 byte dengan validasi alamat.</summary>
    public bool TryReadPointer(long address, out long value)
    {
        value = 0;
        if (!IsValidInstance(address)) return false;
        try
        {
            value = BitConverter.ToInt64(RawMemory.Read(_process, address, 8), 0);
            return true;
        }
        catch { return false; }
    }

    public bool TryReadInt32(long address, out int value)
    {
        value = 0;
        if (!IsValidInstance(address)) return false;
        try
        {
            value = BitConverter.ToInt32(RawMemory.Read(_process, address, 4), 0);
            return true;
        }
        catch { return false; }
    }

    public bool TryReadByte(long address, out byte value)
    {
        value = 0;
        if (!IsValidInstance(address)) return false;
        try
        {
            value = RawMemory.Read(_process, address, 1)[0];
            return true;
        }
        catch { return false; }
    }

    public bool TryReadFloat(long address, out float value)
    {
        value = 0f;
        if (!IsValidInstance(address)) return false;
        try
        {
            value = BitConverter.ToSingle(RawMemory.Read(_process, address, 4), 0);
            return true;
        }
        catch { return false; }
    }

    // ===== flat reads di module base (offsets-driven, sesuai desain Grand Piece.exe) =====
    // Jalur utama sensor asli: nilai dibaca langsung di exeBase + offset key dari
    // offsets.json — tanpa tree-walk instance by-name. Kunci GPO-spesifik tidak ada di
    // dump publik sehingga jalur ini inert sampai provider mengirimkannya; tidak pernah throw.

    public bool TryReadModuleByte(string offsetKey, out byte value)
    {
        value = 0;
        return IsAttached && TryResolve(offsetKey, out long off) && TryReadByte(_exeBase + off, out value);
    }

    public bool TryReadModuleInt32(string offsetKey, out int value)
    {
        value = 0;
        return IsAttached && TryResolve(offsetKey, out long off) && TryReadInt32(_exeBase + off, out value);
    }

    public bool TryReadModuleFloat(string offsetKey, out float value)
    {
        value = 0f;
        return IsAttached && TryResolve(offsetKey, out long off) && TryReadFloat(_exeBase + off, out value);
    }

    public bool TryReadModulePointer(string offsetKey, out long value)
    {
        value = 0;
        return IsAttached && TryResolve(offsetKey, out long off) && TryReadPointer(_exeBase + off, out value);
    }

    /// <summary>
    /// Versi build Roblox yang di-attach: nama folder "version-…" dari path image proses
    /// (QueryFullProcessImageName — andal juga untuk launcher pihak ketiga seperti Fishstrap,
    /// dipakai pola RobloxInstallProbe SupaZero; fallback Process.MainModule). null bila gagal.
    /// Dipakai untuk version check terhadap OffsetsSnapshot.RobloxVersion.
    /// </summary>
    public string? GetRobloxVersion()
    {
        try
        {
            string? path = null;
            if (_attacher.Handle != 0)
            {
                var sb = new StringBuilder(0x1000);
                int size = sb.Capacity;
                if (QueryFullProcessImageName(_attacher.Handle, 0, sb, ref size))
                    path = sb.ToString();
            }
            if (string.IsNullOrEmpty(path))
            {
                using var proc = System.Diagnostics.Process.GetProcessById(_attacher.ProcessId);
                path = proc.MainModule?.FileName;
            }
            var dir = string.IsNullOrEmpty(path) ? null : System.IO.Path.GetDirectoryName(path);
            var name = string.IsNullOrEmpty(dir) ? null : System.IO.Path.GetFileName(dir.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar));
            return !string.IsNullOrEmpty(name) &&
                   name.StartsWith("version-", StringComparison.OrdinalIgnoreCase)
                ? name
                : null;
        }
        catch { return null; }
    }

    // ====== resolusi offset (dot-notation) =====

    public bool TryResolve(string name, out long offset) => _offsets.TryResolve(name, out offset);

    internal int StringLengthOffset()
    {
        return _offsets.TryResolve("StringLength", out long o) && o > 0 && o <= 0x100
            ? (int)o
            : 0x40; // default panjang string Roblox
    }

    internal string ReadRobloxString(long address, int lengthOffset)
    {
        if (!IsValidInstance(address)) return string.Empty;
        try
        {
            if (!TryReadInt32(address + lengthOffset, out int length) || length <= 0 || length > 1024)
                return string.Empty;
            byte[] buf;
            if (TryReadBytes(address, length * 2, out buf))
            {
                return DecodeUtf16(buf);
            }
            if (TryReadPointer(address, out long bufPtr) && IsValidInstance(bufPtr) &&
                TryReadBytes(bufPtr, length * 2, out buf))
            {
                return DecodeUtf16(buf);
            }
            return string.Empty;
        }
        catch { return string.Empty; }
    }

    // ===== rantai engine (kamera live; offset per-instance dari dump nyata) =====

    /// <summary>DataModel: exeBase + FakeDataModel.Pointer → FakeData → +RealDataModel → DataModel.</summary>
    public long ReadDataModel()
    {
        if (!IsAttached) return 0;
        if (!_offsets.TryResolve("FakeDataModel.Pointer", out long ptrOff) &&
            !_offsets.TryResolve("FakeDataModelPointer", out ptrOff))
            return 0;
        if (!TryReadPointer(_exeBase + ptrOff, out long fake) || !IsValidInstance(fake))
            return 0;
        if (!_offsets.TryResolve("FakeDataModel.RealDataModel", out long realOff) &&
            !_offsets.TryResolve("RealDataModel", out realOff))
            return 0;
        return TryReadPointer(fake + realOff, out long real) && IsValidInstance(real) ? real : 0;
    }

    /// <summary>Workspace: DataModel → pointer di DataModel.Workspace (offset dump nyata).</summary>
    public long ReadWorkspace()
    {
        if (!IsAttached) return 0;
        long dm = ReadDataModel();
        if (dm == 0 || !_offsets.TryResolve("DataModel.Workspace", out long off)) return 0;
        return TryReadPointer(dm + off, out long ws) && IsValidInstance(ws) ? ws : 0;
    }

    /// <summary>Camera live: Workspace → pointer di Workspace.CurrentCamera (offset dump nyata).</summary>
    public long ReadCurrentCamera()
    {
        long ws = ReadWorkspace();
        if (ws == 0 || !_offsets.TryResolve("Workspace.CurrentCamera", out long off)) return 0;
        return TryReadPointer(ws + off, out long cam) && IsValidInstance(cam) ? cam : 0;
    }

    /// <summary>Test hook: pasang reader ke address space palsu tanpa proses nyata.</summary>
    internal void AttachForTests(nint process, long moduleBase)
    {
        _process = process;
        _exeBase = moduleBase;
        _attached = true;
    }

    // ===== Roblox string reader (UTF-16, std::string layout) =====

    public bool TryReadBytes(long address, int count, out byte[] value)
    {
        value = Array.Empty<byte>();
        if (!IsValidInstance(address) || count <= 0) return false;
        try
        {
            var buf = RawMemory.Read(_process, address, count);
            if (buf.Length != count) return false;
            value = buf;
            return true;
        }
        catch { return false; }
    }

    private static string DecodeUtf16(byte[] bytes)
    {
        // Berhenti di karakter nol-ganda UTF-16 (00 00), bukan byte tinggi 0 karakter ASCII.
        int len = 0;
        while (len + 1 < bytes.Length && !(bytes[len] == 0 && bytes[len + 1] == 0)) len += 2;
        return len == 0 ? string.Empty : Encoding.Unicode.GetString(bytes, 0, len);
    }

    // ===== module base helper =====

    private static long MainModuleBase(int pid)
    {
        try
        {
            using var proc = System.Diagnostics.Process.GetProcessById(pid);
            return (long)(proc.MainModule?.BaseAddress ?? 0);
        }
        catch { return 0; }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool QueryFullProcessImageName(nint hProcess, int dwFlags, StringBuilder lpExeName, ref int lpdwSize);

    public void Dispose() => Detach();
}

/// <summary>Lookup descendant murni (tanpa memori proses) untuk tes — port dari Inst_FindDescendantWhere.</summary>
public static class InstanceDescendantSearch
{
    public const int MaxIterations = 60000;
    public const int MaxExpandDepth = 0x80;

    public static long FindFirstDescendantOfClass(
        long root,
        string className,
        Func<long, bool> isValid,
        Func<long, IEnumerable<long>> getChildren,
        Func<long, string> getClassName)
    {
        if (!isValid(root)) return 0;
        var queue = new Queue<(long Node, int Depth)>();
        var visited = new HashSet<long>();
        queue.Enqueue((root, 0));
        int iter = 0;
        while (queue.Count > 0 && iter < MaxIterations)
        {
            iter++;
            var (node, depth) = queue.Dequeue();
            if (!visited.Add(node)) continue;
            if (string.Equals(getClassName(node), className, StringComparison.OrdinalIgnoreCase))
                return node;
            if (depth < MaxExpandDepth)
                foreach (long child in getChildren(node))
                    queue.Enqueue((child, depth + 1));
        }
        return 0;
    }
}
