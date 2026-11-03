namespace SupaGPO.Presentation.ViewModels;

/// <summary>
/// Shell utama meniru Grand Piece.exe: tab Halloween / Fishing / Webhooks + strip status
/// koneksi. Timer UI memanggil <see cref="RefreshCurrent"/> ~4 Hz agar status realtime.
/// </summary>
public class MainWindowViewModel : ObservableObject
{
    private int _selectedTabIndex = 1; // buka di tab Fishing

    public List<TabViewModel> Tabs { get; } = new()
    {
        new("Halloween", new PlaceholderViewModel("Halloween",
            "Seasonal event automation — coming soon.")),
        new("Fishing", new FishingViewModel()),
        new("Webhooks", new WebhookViewModel()),
    };

    public int SelectedTabIndex
    {
        get => _selectedTabIndex;
        set
        {
            Set(ref _selectedTabIndex, Math.Clamp(value, 0, Tabs.Count - 1));
            OnPropertyChanged(nameof(HeaderTitle));
            OnPropertyChanged(nameof(CurrentContent));
        }
    }

    public string HeaderTitle => Tabs.Count > 0 ? Tabs[SelectedTabIndex].Title : "";

    public IPageViewModel CurrentContent => Tabs.Count > 0 ? Tabs[SelectedTabIndex].Content : Tabs[0].Content;

    public bool ShowHeader => true;

    public bool IsAttached => App.Host.IsAttached;

    public string ConnectionText => App.Host.IsAttached
        ? $"Roblox: attached (PID {App.Host.ProcessId}) — {App.Host.VersionStatus.Replace("Version check: ", "")}"
        : "Roblox: not running — launch Grand Piece Online, then press Attach";

    public string RunStateText => App.Host.Macro.Running ? "Running" : "Stopped";

    public void RefreshCurrent()
    {
        OnPropertyChanged(nameof(IsAttached));
        OnPropertyChanged(nameof(ConnectionText));
        OnPropertyChanged(nameof(RunStateText));
        if (Tabs.Count > 0)
            Tabs[SelectedTabIndex].Content.Refresh();
    }
}
