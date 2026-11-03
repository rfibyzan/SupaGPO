using System.Diagnostics;

namespace SupaGPO.Core.Settings;

/// <summary>
/// Auto-saves preferences on change with a short debounce. Hook AppSettings.Changed.
/// </summary>
public sealed class SettingsAutoSaver : IDisposable
{
    private readonly AppSettings _settings;
    private readonly JsonPreferenceStore _store;
    private readonly Timer _timer;
    private bool _dirty;

    public SettingsAutoSaver(AppSettings settings, JsonPreferenceStore store, TimeSpan? debounce = null)
    {
        _settings = settings;
        _store = store;
        _timer = new Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
        _settings.Changed += OnChanged;
        debounce ??= TimeSpan.FromMilliseconds(800);
    }

    private void OnChanged(string _)
    {
        _dirty = true;
        _timer.Change(800, Timeout.Infinite);
    }

    private void Flush()
    {
        if (!_dirty) return;
        _dirty = false;
        try { _store.Save(_settings); }
        catch { /* best effort; never crash the app on save failure */ }
    }

    public void SaveNow() => Flush();

    public void Dispose()
    {
        _settings.Changed -= OnChanged;
        _timer.Dispose();
    }
}
