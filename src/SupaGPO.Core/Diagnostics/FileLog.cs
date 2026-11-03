namespace SupaGPO.Core.Diagnostics;

/// <summary>
/// Minimal rotating file logger. Writes to %APPDATA%\SupaGPO\logs\supagpo-{date}.log.
/// Never throws.
/// </summary>
public static class FileLog
{
    private static readonly object _lock = new();
    private static string? _currentDate;
    private static string? _currentPath;

    public static void Line(string message)
    {
        try
        {
            Write(message);
        }
        catch { }
    }

    public static void Error(string context, Exception ex)
    {
        try { Write($"[ERROR] {context}: {ex}"); }
        catch { }
    }

    private static void Write(string message)
    {
        string today = DateTime.UtcNow.ToString("yyyy-MM-dd");
        lock (_lock)
        {
            if (_currentDate != today || string.IsNullOrEmpty(_currentPath))
            {
                _currentDate = today;
                var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "SupaGPO", "logs");
                Directory.CreateDirectory(dir);
                _currentPath = Path.Combine(dir, $"supagpo-{today}.log");
            }
            File.AppendAllText(_currentPath, $"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
    }
}
