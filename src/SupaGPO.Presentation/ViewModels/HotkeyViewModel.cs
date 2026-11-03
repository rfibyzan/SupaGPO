namespace SupaGPO.Presentation.ViewModels;

public class HotkeyViewModel : ObservableObject, IPageViewModel
{
    private readonly RuntimeHost _host = App.Host;

    public string HotkeyName
    {
        get => _host.Settings.General.MacroHotkey;
        set
        {
            _host.Settings.General = _host.Settings.General with { MacroHotkey = value };
            OnPropertyChanged();
        }
    }

    public string CurrentKey => $"{HotkeyName} (vk {_host.Hotkey.VirtualKey:X2})";
    public bool IsPressed => _host.Hotkey.IsHotkeyPressed();

    public void Refresh() => OnPropertyChanged(null);
}
