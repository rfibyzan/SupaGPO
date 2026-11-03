using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace SupaGPO.Presentation;

public sealed class RuntimeHost : IDisposable
{
    private readonly JsonPreferenceStore _store;
    private readonly SettingsAutoSaver _autoSaver;
    private readonly ProcessAttacher _attacher = new();
    private readonly InputDriver _input = new();
    private readonly WebhookDispatcher _webhook;
    private readonly Core.Overlay.OverlayViewModel _overlayVm = new();
    private OffsetsSnapshot _offsets;
    private readonly OffsetsUpdateService _offsetsUpdate = new(new HttpClient());
    private readonly string _offsetsFilePath;
    private readonly GdiGameScreen _screen = new();
    private readonly RobloxMemoryReader _reader;
    private readonly GpoCharacterReader _characterReader;
    private readonly GpoNavigator _navigator;
    private readonly IFishingSensors _sensors;
    private readonly GpoFishingMacro _macro;
    private HotkeyService _hotkey;
    private readonly Timer _hotkeyTimer;

    public AppSettings Settings { get; }
    public ProcessAttacher Attacher => _attacher;
    public InputDriver Input => _input;
    public WebhookDispatcher Webhook => _webhook;
    public Core.Overlay.OverlayViewModel OverlayVm => _overlayVm;
    public OffsetsSnapshot Offsets => _offsets;
    public OffsetsUpdateService OffsetsUpdateService => _offsetsUpdate;
    public string OffsetsFilePath => _offsetsFilePath;
    public RobloxMemoryReader Reader => _reader;
    public IFishingSensors Sensors => _sensors;
    public GpoFishingMacro Macro => _macro;
    public HotkeyService Hotkey => _hotkey;

    public bool IsAttached => _reader.IsAttached;
    public int ProcessId => _reader.ProcessId;

    public RuntimeHost()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var settingsDir = Path.Combine(appData, \"SupaGPO\");
        _offsetsFilePath = Path.Combine(settingsDir, \"offsets.json\");
        _store = new JsonPreferenceStore(settingsDir);
        Settings = _store.Load();
        _autoSaver = new SettingsAutoSaver(Settings, _store);
        MigratePrefs();

        _offsets = OffsetsSnapshot.LoadFromFile(_offsetsFilePath)
            ?? OffsetsSnapshot.Load(\"{}");
        var providerUrl = !string.IsNullOrWhiteSpace(Settings.Offsets.ProviderUrl)
            ? Settings.Offsets.ProviderUrl
            : OffsetsSettings.DefaultProviderUrl;
        if (_offsets.Count == 0 && !string.IsNullOrWhiteSpace(providerUrl))
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    var (ok, msg) = await UpdateAndSaveOffsetsAsync(providerUrl);
                    AppLog.Line($\"Auto offsets result: ok={ok}, msg={msg}\");
                }
                catch (Exception ex)
                {
                    AppLog.Error(\"Auto offsets\", ex);
                }
            });
        }

        _webhook = new WebhookDispatcher(new HttpClient());
        _reader = new RobloxMemoryReader(_attacher, _offsets);
        _characterReader = new GpoCharacterReader(_reader);
        _navigator = new GpoNavigator(_input, _characterReader);
        _sensors = new RobloxFishingSensors(_reader, visualReel: null, () => Settings.Loot);
        var debug = new ReelDebugDumper(Path.Combine(settingsDir, \"debug\"));
        _macro = new GpoFishingMacro(_input, _screen, () => Settings.Fishing,
            () => (uint)Math.Max(0, ProcessId), () => Environment.TickCount64,
            autoThread: true, debug: debug,
            characterReader: _characterReader, navigator: _navigator);
        _macro.StatusChanged += () =>
        {
            double v = _macro.Phase == FishingPhase.Reel ? _macro.LastProgress * 100 : -1;
            _overlayVm.Set(\"FISH\", _macro.StatusText, false, v);
        };

        _hotkey = new HotkeyService(Settings.General.MacroHotkey);

        _hotkeyTimer = new Timer(_ => PollHotkey(), null, Timeout.Infinite, Timeout.Infinite);
        _hotkeyTimer.Change(0, 50);

        AttachRoblox();
        if (IsAttached && Settings.Fishing.AutoRefillBait)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(2000);
                    AppLog.Line(\"DEBUG: Auto-triggering bait test from constructor\");
                    await TestBaitRefillAsync();
                }
                catch (Exception ex)
                {
                    AppLog.Line($\"DEBUG: Auto-test exception: {ex.Message}\");
                }
            });
        }

        _attachTimer = new Timer(_ => TryAutoAttach(), null, 2000, 2000);
    }

    private readonly Timer _attachTimer;

    private void MigratePrefs()
    {
        if (Settings.PrefsVersion >= 1) return;
        if (Settings.General.MacroHotkey == \"F1\")
            Settings.General = Settings.General with { MacroHotkey = \"F3\" };
        if (Settings.Fishing.EquipOnStart)
            Settings.Fishing = Settings.Fishing with { EquipOnStart = false };
        Settings.PrefsVersion = 1;
        AppLog.Line(\"Preferences migrated (hotkey F3, equip-on-start off).\");
    }

    private void TryAutoAttach()
    {
        if (IsAttached) return;
        if (_reader.TryAttach(out _))
        {
            AppLog.Line($\"Auto-attach OK. {VersionStatus}\");
            StatusChanged?.Invoke();
        }
    }

    public void Toggle() => _macro.Toggle();

    public string MacroHotkey
    {
        get => Settings.General.MacroHotkey;
        set
        {
            value = string.IsNullOrWhiteSpace(value) ? \"F3\" : value.Trim().ToUpperInvariant();
            Settings.General = Settings.General with { MacroHotkey = value };
            _hotkey = new HotkeyService(value);
        }
    }

    public bool OverlayVisible
    {
        get => Settings.General.ShowOverlay;
        set
        {
            if (Settings.General.ShowOverlay == value) return;
            Settings.General = Settings.General with { ShowOverlay = value };
            OverlayVisibilityChanged?.Invoke(value);
        }
    }

    public event Action<bool>? OverlayVisibilityChanged;

    public void SetOverlayVisible(bool visible) => OverlayVisible = visible;

    public void AttachRoblox()
    {
        var ok = _reader.TryAttach(out var status);
        AppLog.Line($\"Attach: {status}\");
        AppLog.Line(VersionStatus);
        if (ok) StatusChanged?.Invoke();
    }

    public string VersionStatus
    {
        get
        {
            if (!IsAttached) return \"Version check: Roblox not attached.\";
            string live = _reader.GetRobloxVersion();
            if (string.IsNullOrEmpty(live)) return \"Version check: Roblox version unknown.\";
            if (!_offsets.HasVersionInfo)
                return $\"Version check: offsets carry no version info (Roblox {live}).\";
            return _offsets.MatchesRobloxVersion(live)
                ? $\"Version check: offsets match Roblox {live}.\"
                : $\"WARNING: Roblox version changed - update offsets! (offsets target {_offsets.RobloxVersion}, Roblox is {live})\";
        }
    }

    public void DetachRoblox() => _reader.Detach();

    public bool ReloadOffsets()
    {
        var loaded = OffsetsSnapshot.LoadFromFile(_offsetsFilePath);
        if (loaded is null) return false;
        _offsets = loaded;
        _reader.OffsetsSnapshot = loaded;
        AppLog.Line($\"Offsets reloaded from {_offsetsFilePath} ({loaded.Count} entries).\");
        AppLog.Line(VersionStatus);
        return true;
    }

    public async Task<(bool Ok, string Message)> UpdateAndSaveOffsetsAsync(string url = null)
    {
        var target = string.IsNullOrWhiteSpace(url)
            ? Settings.Offsets.ProviderUrl
            : url;
        if (string.IsNullOrWhiteSpace(target))
            return (false, \"No offsets URL configured.\");
        var (ok, msg) = await _offsetsUpdate.TryUpdateAndSaveAsync(target, _offsetsFilePath);
        if (ok) ReloadOffsets();
        return (ok, msg);
    }

    public async Task TestBaitRefillAsync()
    {
        AppLog.Line(\"TEST BAIT: === METHOD ENTRY ===\");
        if (!IsAttached) { AppLog.Line(\"TEST BAIT: Roblox not attached\"); return; }
        if (_macro.Running) { AppLog.Line(\"TEST BAIT: Macro is running, stop first\"); return; }
        var s = Settings.Fishing with { AutoWalkToBait = false };
        if (!s.AutoRefillBait) { AppLog.Line(\"TEST BAIT: AutoRefillBait is disabled\"); return; }
        AppLog.Line($\"TEST BAIT: CharacterReader={_characterReader.GetHashCode()} (walk disabled for test)\");
        var debug = new ReelDebugDumper(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), \"SupaGPO\", \"debug\"));
        var controller = new BaitRefillController(_input, _screen, () => s, _characterReader, _navigator, debug);
        long now = Environment.TickCount64; controller.Start(now);
        
        AppLog.Line(\"TEST BAIT: Calling Refresh...\");
        _characterReader.Refresh();
        AppLog.Line(\"TEST BAIT: Refresh returned\");
        var startPos = _characterReader.HasData ? (_characterReader.Position!.Value.X, _characterReader.Position!.Value.Z) : (0.0, 0.0);
        var camLook = _characterReader.CameraLook ?? (0.0, 0.0);
        AppLog.Line($\"TEST BAIT: Start at ({startPos.Item1:0},{startPos.Item2:0}) CamLook=({camLook.Item1:F2},{camLook.Item2:F2}) HasData={_characterReader.HasData}\");
        
        bool finished = false; int stepCount = 0;
        while (!finished && stepCount < 2000)
        {
            now = Environment.TickCount64;
            PixelRect? rect = _screen.GetClientRect(ProcessId);
            if (rect is not PixelRect r) { await Task.Delay(50); continue; }
            
            if (stepCount % 20 == 0)
            {
                _characterReader.Refresh();
                if (_characterReader.HasData)
                {
                    var pos = _characterReader.Position!.Value;
                    var look = _characterReader.CameraLook ?? (0.0, 0.0);
                    AppLog.Line($\"TEST BAIT: Pos=({pos.X:F1},{pos.Z:F1}) Look=({look.Item1:F2},{look.Item2:F2}) State={controller.State}\");
                }
                else
                {
                    AppLog.Line($\"TEST BAIT: No character data! State={controller.State}\");
                }
            }
            
            var (sleep, status) = controller.Step(now, r);
            if (stepCount % 20 == 0) AppLog.Line($\"TEST BAIT: {controller.State} - {status}\");
            if (controller.Finished)
            {
                finished = true;
                if (controller.FullBait) AppLog.Line(\"TEST BAIT: Bait penuh (teks merah), backoff\");
                else if (controller.Failed) AppLog.Line(\"TEST BAIT: Gagal (timeout/tidak ada dialog)\");
                else AppLog.Line(\"TEST BAIT: Selesai berhasil\");
            }
            await Task.Delay(Math.Max(10, sleep)); stepCount++;
        }
        if (!finished) AppLog.Line(\"TEST BAIT: Timeout keseluruhan\");
        _navigator.ReleaseAll();
    }

    private void PollHotkey()
    {
        if (HotkeyCaptureActive) { _hotkeyWasDown = _hotkey.IsHotkeyPressed(); return; }

        bool down = _hotkey.IsHotkeyPressed();
        if (down && !_hotkeyWasDown)
        {
            Toggle();
            AppLog.Line($\"Hotkey toggled -> fishing={_macro.Running}\");
        }
        _hotkeyWasDown = down;

        if (HotkeyService.IsVkDown(0x73))
        {
            if (!_macro.Running)
            {
                _ = Task.Run(async () => await TestBaitRefillAsync());
            }
            else
            {
                AppLog.Line(\"TEST BAIT: Cannot test while macro running (press F3 to stop first)\");
            }
            Thread.Sleep(300);
        }

        if (HotkeyService.IsVkDown(0x74))
        {
            _characterReader.Refresh();
            if (_characterReader.HasData)
            {
                var pos = _characterReader.Position!.Value;
                var look = _characterReader.CameraLook ?? (0.0, 0.0);
                AppLog.Line($\"DEBUG: Pos=({pos.X:F1},{pos.Y:F1},{pos.Z:F1}) Look=({look.Item1:F2},{look.Item2:F2})\");
            }
            else
            {
                AppLog.Line(\"DEBUG: No character data\");
            }
            Thread.Sleep(300);
        }
    }

    private volatile bool _hotkeyWasDown;
    public volatile bool HotkeyCaptureActive;
    public event Action? StatusChanged;

    public void Dispose()
    {
        _attachTimer.Dispose();
        _macro.Stop();
        _autoSaver.Dispose();
        _reader.Dispose();
        _attacher.Dispose();
    }
}