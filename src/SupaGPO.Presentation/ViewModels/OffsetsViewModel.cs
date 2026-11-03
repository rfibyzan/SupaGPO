using System.Windows.Input;
using SupaGPO.Core.Settings;

namespace SupaGPO.Presentation.ViewModels;

public class OffsetsViewModel : ObservableObject, IPageViewModel
{
    private readonly RuntimeHost _host = App.Host;
    private string _status = "";
    private string _url = "";

    public string Url
    {
        get => _url;
        set => Set(ref _url, value);
    }

    public string Status
    {
        get => _status;
        set => Set(ref _status, value);
    }

    public int OffsetCount => _host.Offsets.Count;

    public string LocalPath => _host.OffsetsFilePath;

    public bool HasLocalOffsets => _host.Offsets.Count > 0;

    public ICommand UpdateCommand { get; }
    public ICommand ReloadCommand { get; }

    public OffsetsViewModel()
    {
        // URL sudah diisi oleh default provider — pengguna tidak perlu mengetik.
        Url = !string.IsNullOrWhiteSpace(_host.Settings.Offsets.ProviderUrl)
            ? _host.Settings.Offsets.ProviderUrl
            : OffsetsSettings.DefaultProviderUrl;
        Status = _host.VersionStatus;
        UpdateCommand = new RelayCommand(async _ => { await UpdateAsync(); });
        ReloadCommand = new RelayCommand(_ => Reload());
    }

    private async Task UpdateAsync()
    {
        var target = string.IsNullOrWhiteSpace(Url)
            ? (!string.IsNullOrWhiteSpace(_host.Settings.Offsets.ProviderUrl)
                ? _host.Settings.Offsets.ProviderUrl
                : OffsetsSettings.DefaultProviderUrl)
            : Url;
        if (string.IsNullOrWhiteSpace(target))
        { Status = "Enter an offsets.json URL."; return; }
        Status = "Updating...";
        var (ok, msg) = await _host.UpdateAndSaveOffsetsAsync(target);
        Status = ok ? $"{msg} — {_host.VersionStatus}" : msg;
        OnPropertyChanged(nameof(HasLocalOffsets));
        OnPropertyChanged(nameof(OffsetCount));
    }

    private void Reload()
    {
        var ok = _host.ReloadOffsets();
        Status = ok
            ? $"Reloaded from {_host.OffsetsFilePath} ({_host.Offsets.Count} entries) — {_host.VersionStatus}"
            : "No local offsets file found.";
        OnPropertyChanged(nameof(HasLocalOffsets));
        OnPropertyChanged(nameof(OffsetCount));
    }

    public void Refresh() => OnPropertyChanged(null);
}
