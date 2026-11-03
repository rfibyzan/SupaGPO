using System.Windows.Input;

namespace SupaGPO.Presentation.ViewModels;

public class FishingViewModel : ObservableObject, IPageViewModel
{
    private readonly RuntimeHost _host = App.Host;
    private int _subTab;
    private string _offsetsStatus = "";

    public List<string> SubTabs { get; } = new() { "General", "Auto buy leg bait (Trading Hub)" };

    public int SubTab
    {
        get => _subTab;
        set
        {
            if (Set(ref _subTab, Math.Clamp(value, 0, SubTabs.Count - 1)))
            {
                OnPropertyChanged(nameof(IsGeneral));
                OnPropertyChanged(nameof(IsLegBait));
            }
        }
    }

    public bool IsGeneral => SubTab == 0;
    public bool IsLegBait => SubTab == 1;

    public ICommand ToggleCommand { get; }
    public ICommand AttachCommand { get; }
    public ICommand UpdateOffsetsCommand { get; }
    public ICommand TestBaitRefillCommand { get; }

    public FishingViewModel()
    {
        ToggleCommand = new RelayCommand(_ => _host.Toggle());
        AttachCommand = new RelayCommand(_ => { _host.AttachRoblox(); Refresh(); });
        ChangeKeybindCommand = new RelayCommand(_ => BeginKeybindCapture());
        UpdateOffsetsCommand = new RelayCommand(async _ =>
        {
            OffsetsStatus = "Updating offsets...";
            var (ok, msg) = await _host.UpdateAndSaveOffsetsAsync();
            OffsetsStatus = msg;
            OnPropertyChanged(nameof(VersionText));
        });
        TestBaitRefillCommand = new RelayCommand(async _ =>
        {
            OffsetsStatus = "Testing bait refill...";
            await _host.TestBaitRefillAsync();
            OffsetsStatus = "Test bait refill completed";
        });
    }

    public bool Running => _host.Macro.Running;
    public string ToggleText => Running ? "Stop fishing" : $"Start fishing Â· {_host.Settings.General.MacroHotkey}";
    public string StatusText => _host.Macro.StatusText;
    public string SessionText => _host.Macro.SessionText;
    public string VersionText => _host.VersionStatus.Replace("Version check: ", "");

    public string OffsetsStatus
    {
        get => _offsetsStatus;
        set => Set(ref _offsetsStatus, value);
    }

    public int RodSlot
    {
        get => _host.Settings.Fishing.RodSlot;
        set { _host.Settings.Fishing = _host.Settings.Fishing with { RodSlot = (int)value }; OnPropertyChanged(); }
    }

    public bool EquipOnStart
    {
        get => _host.Settings.Fishing.EquipOnStart;
        set { _host.Settings.Fishing = _host.Settings.Fishing with { EquipOnStart = value }; OnPropertyChanged(); }
    }

    public bool ShowOverlay
    {
        get => _host.OverlayVisible;
        set { _host.OverlayVisible = value; OnPropertyChanged(); }
    }

    private bool _bindingKey;
    private Avalonia.Threading.DispatcherTimer? _bindTimer;

    public bool BindingKey
    {
        get => _bindingKey;
        private set { if (Set(ref _bindingKey, value)) OnPropertyChanged(nameof(KeybindButtonText)); }
    }

    public string KeybindButtonText => BindingKey
        ? "Press any key... (Esc cancels)"
        : $"Change keybind ({_host.Settings.General.MacroHotkey})";

    public ICommand ChangeKeybindCommand { get; }

    public string MacroHotkey
    {
        get => _host.MacroHotkey;
        set { _host.MacroHotkey = value; OnPropertyChanged(); OnPropertyChanged(nameof(ToggleText)); OnPropertyChanged(nameof(KeybindButtonText)); }
    }

    private void BeginKeybindCapture()
    {
        if (BindingKey) return;
        BindingKey = true;
        _host.HotkeyCaptureActive = true;
        _bindTimer = new Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(30)
        };
        _bindTimer.Tick += OnBindTick;
        _bindTimer.Start();
    }

    private void OnBindTick(object? sender, EventArgs e)
    {
        if (SupaGPO.Core.Input.HotkeyService.IsVkDown(0x1B)) { EndKeybindCapture(null); return; }
        foreach (ushort vk in SupaGPO.Core.Input.HotkeyMap.CapturableKeys)
        {
            if (SupaGPO.Core.Input.HotkeyService.IsVkDown(vk))
            {
                EndKeybindCapture(SupaGPO.Core.Input.HotkeyMap.Name(vk));
                return;
            }
        }
    }

    private void EndKeybindCapture(string? key)
    {
        if (_bindTimer is not null) { _bindTimer.Stop(); _bindTimer.Tick -= OnBindTick; _bindTimer = null; }
        _host.HotkeyCaptureActive = false;
        BindingKey = false;
        if (key is not null) MacroHotkey = key;
        Refresh();
    }

    public int BiteTimeoutSec
    {
        get => _host.Settings.Fishing.BiteTimeoutSec;
        set { _host.Settings.Fishing = _host.Settings.Fishing with { BiteTimeoutSec = (int)value }; OnPropertyChanged(); }
    }

    public bool AutoRefillBait
    {
        get => _host.Settings.Fishing.AutoRefillBait;
        set { _host.Settings.Fishing = _host.Settings.Fishing with { AutoRefillBait = value }; OnPropertyChanged(); }
    }

    public bool AutoWalkToBait
    {
        get => _host.Settings.Fishing.AutoWalkToBait;
        set { _host.Settings.Fishing = _host.Settings.Fishing with { AutoWalkToBait = value }; OnPropertyChanged(); }
    }

    public int WalkTimeoutSec
    {
        get => _host.Settings.Fishing.WalkTimeoutSec;
        set { _host.Settings.Fishing = _host.Settings.Fishing with { WalkTimeoutSec = (int)value }; OnPropertyChanged(); }
    }

    public int RefillAmount
    {
        get => _host.Settings.Fishing.RefillAmount;
        set { _host.Settings.Fishing = _host.Settings.Fishing with { RefillAmount = (int)value }; OnPropertyChanged(); }
    }

    public int RefillEvery
    {
        get => _host.Settings.Fishing.RefillEvery;
        set { _host.Settings.Fishing = _host.Settings.Fishing with { RefillEvery = (int)value }; OnPropertyChanged(); }
    }

    public bool AutoBuyLegBait
    {
        get => _host.Settings.Fishing.AutoBuyLegBait;
        set { _host.Settings.Fishing = _host.Settings.Fishing with { AutoBuyLegBait = value }; OnPropertyChanged(); }
    }

    public void Refresh()
    {
        OnPropertyChanged(nameof(Running));
        OnPropertyChanged(nameof(ToggleText));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(SessionText));
        OnPropertyChanged(nameof(VersionText));
    }
}