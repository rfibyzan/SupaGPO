namespace SupaGPO.Presentation.ViewModels;

public class OverlayViewModel : ObservableObject, IPageViewModel
{
    private readonly RuntimeHost _host = App.Host;

    public bool OverlayVisible
    {
        get => _host.Settings.General.ShowOverlay;
        set { _host.SetOverlayVisible(value); OnPropertyChanged(); }
    }

    public int LineCount => _host.OverlayVm.Lines.Count;

    public string LatestLine => _host.OverlayVm.Lines.Count > 0
        ? _host.OverlayVm.Lines[^1] : "—";

    public System.Windows.Input.ICommand ClearCommand { get; }

    public OverlayViewModel() => ClearCommand = new RelayCommand(_ =>
    {
        Clear();
        Refresh();
    });

    public void Clear() => _host.OverlayVm.Lines.Clear();

    public void Refresh() => OnPropertyChanged(null);
}
