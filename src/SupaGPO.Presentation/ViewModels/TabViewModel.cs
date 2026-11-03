namespace SupaGPO.Presentation.ViewModels;

/// <summary>Halaman yang bisa di-refresh oleh timer UI (status realtime).</summary>
public interface IPageViewModel
{
    void Refresh();
}

/// <summary>Satu tab navigasi utama (judul + konten).</summary>
public sealed class TabViewModel
{
    public string Title { get; }
    public IPageViewModel Content { get; }

    public TabViewModel(string title, IPageViewModel content)
    {
        Title = title;
        Content = content;
    }
}
