namespace SupaGPO.Presentation.ViewModels;

/// <summary>Halaman placeholder untuk fitur yang belum tersedia (mis. tab Halloween).</summary>
public class PlaceholderViewModel : ObservableObject, IPageViewModel
{
    public string Title { get; }
    public string Text { get; }

    public PlaceholderViewModel(string title, string text)
    {
        Title = title;
        Text = text;
    }

    public void Refresh() { }
}
