namespace SupaGPO.Core.Overlay;

/// <summary>
/// Pushes FishingLoop status into an OverlayViewModel. The Presentation layer owns the Avalonia
/// window; Core only owns the view-model + this renderer.
/// </summary>
public sealed class OverlayRenderer
{
    private readonly OverlayViewModel _vm;

    public OverlayRenderer(OverlayViewModel vm) => _vm = vm;

    public void Publish(Fishing.FishingStatus? status)
    {
        if (status is null) return;
        _vm.Set(status.Category, status.Text, status.FlagA, status.Value);
    }
}
