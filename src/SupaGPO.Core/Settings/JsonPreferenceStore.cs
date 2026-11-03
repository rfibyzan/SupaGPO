using System.Text.Json;

namespace SupaGPO.Core.Settings;

public sealed class JsonPreferenceStore(string directory)
{
    /// <summary>After loading, normalize empty ProviderUrl to the default so users never have to type it.</summary>
    private static AppSettings Normalize(AppSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Offsets.ProviderUrl))
            settings.Offsets = settings.Offsets with { ProviderUrl = OffsetsSettings.DefaultProviderUrl };
        return settings;
    }
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    private readonly string _file = Path.Combine(directory, "preferences.json");

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(_file))
            {
                var loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_file), Options);
                if (loaded is not null) return Normalize(loaded);
            }
        }
        catch { }
        return new AppSettings();
    }

    public string FilePath => _file;

    public static string Serialize(AppSettings settings) => JsonSerializer.Serialize(settings, Options);

    public void Save(AppSettings settings) => SaveJson(Serialize(settings));

    public void SaveJson(string json)
    {
        Directory.CreateDirectory(directory);
        string temp = _file + ".tmp";
        try
        {
            File.WriteAllText(temp, json);
            File.Move(temp, _file, overwrite: true);
        }
        catch
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
            throw;
        }
    }
}
