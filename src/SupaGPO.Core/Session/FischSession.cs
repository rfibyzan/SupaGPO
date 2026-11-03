using SupaGPO.Core.Process;

namespace SupaGPO.Core.Session;

/// <summary>
/// Production session handle: ProcessAttacher + a flag whether we own the attach lifetime.
/// </summary>
public sealed class FischSession : IDisposable
{
    public ProcessAttacher Attacher { get; }
    public bool IsAttached => Attacher.IsAttached;
    public int ProcessId => Attacher.ProcessId;

    public FischSession(ProcessAttacher attacher)
    {
        Attacher = attacher;
    }

    public void Dispose() => Attacher.Dispose();
}
