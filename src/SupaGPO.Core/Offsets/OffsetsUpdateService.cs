using System.Net.Http;

namespace SupaGPO.Core.Offsets;

/// <summary>
/// Offsets update service: fetches offsets.json from a provider URL, validates it parses,
/// then atomically replaces the local file. Mirrors heritage validate-then-replace rule.
/// </summary>
public sealed class OffsetsUpdateService
{
    private readonly HttpClient _http;

    public OffsetsUpdateService(HttpClient http) => _http = http;

    /// <summary>Fetch and validate offsets from <paramref name="url"/> without saving.</summary>
    public async Task<(bool Ok, string Message)> TryUpdateAsync(string url,
        CancellationToken cancel = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return (false, "No offsets URL configured.");
        try
        {
            string json = await _http.GetStringAsync(url, cancel);
            // Validate it parses before touching the local file.
            _ = OffsetsSnapshot.Load(json);
            return (true, "Offsets validated.");
        }
        catch (OperationCanceledException) { return (false, "Update cancelled."); }
        catch (Exception ex)
        {
            return (false, "Offset update failed: " + ex.Message);
        }
    }

    /// <summary>Fetch, validate, then atomically save offsets to <paramref name="savePath"/>.</summary>
    public async Task<(bool Ok, string Message)> TryUpdateAndSaveAsync(string url, string savePath,
        CancellationToken cancel = default)
    {
        if (string.IsNullOrWhiteSpace(url))
            return (false, "No offsets URL configured.");
        try
        {
            string json = await _http.GetStringAsync(url, cancel);
            var snap = OffsetsSnapshot.Load(json); // validate
            snap.SaveToFile(savePath);               // atomic replace
            return (true, $"Offsets saved to {savePath} ({snap.Count} entries).");
        }
        catch (OperationCanceledException) { return (false, "Update cancelled."); }
        catch (Exception ex)
        {
            return (false, "Offset update failed: " + ex.Message);
        }
    }
}
