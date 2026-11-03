namespace SupaGPO.Core.Diagnostics;

/// <summary>
/// Thin log facade. Presentation installs the real file sink; Core calls here.
/// </summary>
public static class AppLog
{
    public static Action<string>? LineSink;
    public static Action<string, Exception>? ErrorSink;

    public static void Line(string message) => LineSink?.Invoke(message);
    public static void Error(string context, Exception ex) => ErrorSink?.Invoke(context, ex);
}
