using System.Net;
using System.Net.Http;
using System.Threading;
using SupaGPO.Core.Net;

namespace SupaGPO.Core.Tests;

public class WebhookDispatcherTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        public string? LastContent;
        public HttpStatusCode Status = HttpStatusCode.NoContent;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            LastContent = req.Content is not null
                ? new StreamReader(req.Content.ReadAsStream()).ReadToEnd()
                : null;
            return Task.FromResult(new HttpResponseMessage(Status));
        }
    }

    [Fact]
    public void Is_url_valid()
    {
        Assert.True(WebhookDispatcher.IsDiscordWebhookUrl("https://discord.com/api/webhooks/123/abc"));
        Assert.False(WebhookDispatcher.IsDiscordWebhookUrl("https://example.com/"));
    }

    [Fact]
    public async Task Send_test_success()
    {
        var handler = new FakeHandler { Status = HttpStatusCode.NoContent };
        var wh = new WebhookDispatcher(new HttpClient(handler));
        var outcome = await wh.SendTestAsync("https://discord.com/api/webhooks/123/abc");
        Assert.Equal(WebhookSendOutcome.Success, outcome);
        Assert.NotNull(handler.LastContent);
    }

    [Fact]
    public async Task Invalid_url_rejected()
    {
        var wh = new WebhookDispatcher(new HttpClient());
        var outcome = await wh.SendTestAsync("https://example.com/");
        Assert.Equal(WebhookSendOutcome.InvalidUrl, outcome);
    }
}
