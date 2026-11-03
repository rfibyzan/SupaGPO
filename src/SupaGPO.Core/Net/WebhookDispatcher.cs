using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace SupaGPO.Core.Net;

public sealed class DiscordEmbedField(string name, string value, bool inline = false)
{
    [JsonPropertyName("name")] public string Name { get; } = name;
    [JsonPropertyName("value")] public string Value { get; } = value;
    [JsonPropertyName("inline")] public bool Inline { get; } = inline;
}

public sealed class DiscordEmbedFooter(string text)
{
    [JsonPropertyName("text")] public string Text { get; } = text;
}

public sealed class DiscordEmbed(string title, string description,
    DiscordEmbedField[]? fields = null, DiscordEmbedFooter? footer = null, int? color = null,
    string? timestamp = null)
{
    [JsonPropertyName("title")] public string Title { get; } = title;
    [JsonPropertyName("description")] public string Description { get; } = description;
    [JsonPropertyName("fields"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DiscordEmbedField[]? Fields { get; } = fields;
    [JsonPropertyName("footer"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public DiscordEmbedFooter? Footer { get; } = footer;
    [JsonPropertyName("color"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Color { get; } = color;
    [JsonPropertyName("timestamp"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Timestamp { get; } = timestamp;
}

public sealed class DiscordWebhookPayload(string content, DiscordEmbed[] embeds, string? username = null)
{
    [JsonPropertyName("content")] public string Content { get; } = content;
    [JsonPropertyName("embeds")] public DiscordEmbed[] Embeds { get; } = embeds;
    [JsonPropertyName("username"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Username { get; } = username;
}

public enum WebhookSendOutcome { Success, Rejected, Unreachable, InvalidUrl }

public sealed class WebhookDispatcher(HttpClient http)
{
    public const string TestEmbedTitle = "Webhook connected";
    public const string TestEmbedDescription = "SupaGPO notifications are set up correctly.";
    public const int MaxAttempts = 3;
    private const int BackoffBaseMs = 250;

    public static bool IsDiscordWebhookUrl(string url) =>
        url.StartsWith("https://discord.com/api/webhooks/", StringComparison.Ordinal);

    public async Task<WebhookSendOutcome> SendTestAsync(string webhookUrl,
        CancellationToken cancel = default)
    {
        if (!IsDiscordWebhookUrl(webhookUrl)) return WebhookSendOutcome.InvalidUrl;
        try
        {
            bool ok = await TrySendAsync(webhookUrl, TestEmbedTitle, TestEmbedDescription, cancel);
            return ok ? WebhookSendOutcome.Success : WebhookSendOutcome.Rejected;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return WebhookSendOutcome.Unreachable;
        }
    }

    public Task<bool> SendAsync(string webhookUrl, string title, string description,
        CancellationToken cancel = default) =>
        TrySendAsync(webhookUrl, title, description, cancel);

    private Task<bool> TrySendAsync(string webhookUrl, string title, string description,
        CancellationToken cancel) =>
        SendPayloadAsync(webhookUrl, new DiscordWebhookPayload("",
            [new DiscordEmbed(title, description)]), cancel);

    public async Task<bool> SendPayloadAsync(string webhookUrl, DiscordWebhookPayload payload,
        CancellationToken cancel = default)
    {
        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            using var response = await http.PostAsJsonAsync(webhookUrl, payload, cancel);
            if (response.IsSuccessStatusCode) return true;
            int code = (int)response.StatusCode;
            if (code is >= 400 and < 500 && code != 429) return false;
            if (attempt < MaxAttempts)
                await Task.Delay(TimeSpan.FromMilliseconds(BackoffBaseMs * attempt * attempt), cancel);
        }
        return false;
    }
}
