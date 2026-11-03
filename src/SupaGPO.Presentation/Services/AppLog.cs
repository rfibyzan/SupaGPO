namespace SupaGPO.Presentation.Services;

/// <summary>
/// Installs the file-log sink so SupaGPO.Core.Diagnostics.AppLog writes to disk.
/// </summary>
public static class AppLog
{
    public static void Install()
    {
        SupaGPO.Core.Diagnostics.AppLog.LineSink = m =>
        {
            try { SupaGPO.Core.Diagnostics.FileLog.Line(m); } catch { }
        };
        SupaGPO.Core.Diagnostics.AppLog.ErrorSink = (ctx, ex) =>
        {
            try { SupaGPO.Core.Diagnostics.FileLog.Error(ctx, ex); } catch { }
        };
    }
}
