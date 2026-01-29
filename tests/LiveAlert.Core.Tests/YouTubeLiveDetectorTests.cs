using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using LiveAlert.Core;
using Xunit;

namespace LiveAlert.Core.Tests;

public sealed class YouTubeLiveDetectorTests
{
    [Fact]
    public async Task WatchUrlLive_ReturnsLive()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"isLiveNow\":true}")
        });
        var client = new HttpClient(handler);
        var detector = new YouTubeLiveDetector(client);
        var alert = new AlertConfig { Url = "https://www.youtube.com/watch?v=ABCDEFGHIJK" };

        var result = await detector.CheckLiveAsync(alert, CancellationToken.None);

        Assert.True(result.IsLive);
        Assert.Equal("ABCDEFGHIJK", result.VideoId);
    }

    [Fact]
    public async Task ChannelUrlNotLive_ReturnsNotLive()
    {
        var handler = new StubHandler(req =>
        {
            var url = req.RequestUri?.ToString() ?? string.Empty;
            if (url.Contains("/live"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("\"videoId\":\"LIVE1234567\"")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"isLiveNow\":false}")
            };
        });
        var client = new HttpClient(handler);
        var detector = new YouTubeLiveDetector(client);
        var alert = new AlertConfig { Url = "https://www.youtube.com/channel/UC123" };

        var result = await detector.CheckLiveAsync(alert, CancellationToken.None);

        Assert.False(result.IsLive);
    }

    [Fact]
    public async Task LivePageMissingVideoId_ReturnsNotLive()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>No video id here</html>")
        });
        var client = new HttpClient(handler);
        var detector = new YouTubeLiveDetector(client);
        var alert = new AlertConfig { Url = "https://www.youtube.com/channel/UC123" };

        var result = await detector.CheckLiveAsync(alert, CancellationToken.None);

        Assert.False(result.IsLive);
    }

    [Fact]
    public async Task HandlerThrows_ReturnsError()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("boom"));
        var client = new HttpClient(handler);
        var detector = new YouTubeLiveDetector(client);
        var alert = new AlertConfig { Url = "https://www.youtube.com/channel/UC123" };

        var result = await detector.CheckLiveAsync(alert, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.NotNull(result.ErrorMessage);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
