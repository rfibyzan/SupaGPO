using System.Windows.Input;
using SupaGPO.Core.Net;

namespace SupaGPO.Presentation.ViewModels;

public class WebhookViewModel : ObservableObject, IPageViewModel
{
    private readonly RuntimeHost _host = App.Host;
    private string _status = "";

    public string WebhookUrl
    {
        get => _host.Settings.Webhook.WebhookUrl;
        set { _host.Settings.Webhook = _host.Settings.Webhook with { WebhookUrl = value }; OnPropertyChanged(); }
    }

    public bool FishingStarted
    {
        get => _host.Settings.Webhook.FishingStarted;
        set { _host.Settings.Webhook = _host.Settings.Webhook with { FishingStarted = value }; OnPropertyChanged(); }
    }

    public bool FishingStopped
    {
        get => _host.Settings.Webhook.FishingStopped;
        set { _host.Settings.Webhook = _host.Settings.Webhook with { FishingStopped = value }; OnPropertyChanged(); }
    }

    public bool CatchNotify
    {
        get => _host.Settings.Webhook.CatchNotify;
        set { _host.Settings.Webhook = _host.Settings.Webhook with { CatchNotify = value }; OnPropertyChanged(); }
    }

    public string Status
    {
        get => _status;
        set => Set(ref _status, value);
    }

    public ICommand TestCommand { get; }

    public WebhookViewModel() => TestCommand = new RelayCommand(_ => Test());

    public async void Test()
    {
        if (string.IsNullOrWhiteSpace(WebhookUrl))
        { Status = "Enter a Discord webhook URL."; return; }
        var outcome = await _host.Webhook.SendTestAsync(WebhookUrl);
        Status = outcome == WebhookSendOutcome.Success ? "Webhook connected." : "Webhook test failed.";
    }

    public void Refresh() => OnPropertyChanged(null);
}
