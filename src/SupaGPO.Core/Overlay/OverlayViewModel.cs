using System.Collections.ObjectModel;

namespace SupaGPO.Core.Overlay;

/// <summary>ViewModel for the running status overlay (Avalonia window).</summary>
public sealed class OverlayViewModel
{
    public ObservableCollection<string> Lines { get; } = new();

    public void Set(string category, string text, bool flagA, double value)
    {
        string suffix = flagA ? $" [flagA]" : "";
        string val = value >= 0 ? $" ({value:F1}%)" : "";
        string line = $"[{category}] {text}{suffix}{val}";
        if (Lines.Count > 200) Lines.RemoveAt(0);
        Lines.Add(line);
    }

    public void Clear() => Lines.Clear();
}
