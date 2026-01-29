using System.Net;
using System.Net.Http;
using LiveAlert.Core;

namespace LiveAlert.Android.Tests;

public static class YouTubeDetectorTests
{
    public static IEnumerable<TestCase> All()
    {
        yield return new TestCase("YouTube watch url live returns live", WatchUrlLive);
        yield return new TestCase("YouTube channel url not live", ChannelNotLive);
        yield return new TestCase("YouTube live page missing videoId", LivePageMissingVideoId);
        yield return new TestCase("YouTube handler error returns error", HandlerError);
    }

    private static void WatchUrlLive()
    {
        var handler = new StubHandler(req =>
        {
            var html = "{\"isLiveNow\":true}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html)
            };
        });
        var client = new HttpClient(handler);
        var detector = new YouTubeLiveDetector(client);
        var alert = new AlertConfig { Url = "https://www.youtube.com/watch?v=ABCDEFGHIJK" };

        var result = detector.CheckLiveAsync(alert, CancellationToken.None).GetAwaiter().GetResult();
        TestAssertions.True(result.IsLive, "Expected live=true");
        TestAssertions.Equal("ABCDEFGHIJK", result.VideoId ?? string.Empty, "VideoId should match");
    }

    private static void ChannelNotLive()
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

        var result = detector.CheckLiveAsync(alert, CancellationToken.None).GetAwaiter().GetResult();
        TestAssertions.True(!result.IsLive, "Expected not live");
    }

    private static void LivePageMissingVideoId()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>No video id here</html>")
        });
        var client = new HttpClient(handler);
        var detector = new YouTubeLiveDetector(client);
        var alert = new AlertConfig { Url = "https://www.youtube.com/channel/UC123" };

        var result = detector.CheckLiveAsync(alert, CancellationToken.None).GetAwaiter().GetResult();
        TestAssertions.True(!result.IsLive, "Expected not live when no videoId");
    }

    private static void HandlerError()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("boom"));
        var client = new HttpClient(handler);
        var detector = new YouTubeLiveDetector(client);
        var alert = new AlertConfig { Url = "https://www.youtube.com/channel/UC123" };

        var result = detector.CheckLiveAsync(alert, CancellationToken.None).GetAwaiter().GetResult();
        TestAssertions.True(result.IsError, "Expected error result");
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
