using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace LiveAlert.Core;

public interface ILiveDetector
{
    Task<LiveCheckResult> CheckLiveAsync(AlertConfig alert, CancellationToken cancellationToken);
}

public sealed class YouTubeLiveDetector : ILiveDetector
{
    private readonly HttpClient _httpClient;
    private static readonly Regex VideoIdRegex = new("\"videoId\":\"([A-Za-z0-9_-]{11})\"", RegexOptions.Compiled);
    private static readonly Regex IsLiveNowRegex = new("\"isLiveNow\":(true|false)", RegexOptions.Compiled);

    public YouTubeLiveDetector(HttpClient httpClient)
    {
        _httpClient = httpClient;
        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Mozilla", "5.0"));
        }
    }

    public async Task<LiveCheckResult> CheckLiveAsync(AlertConfig alert, CancellationToken cancellationToken)
    {
        try
        {
            var url = alert.Url.Trim();
            if (url.Contains("watch?v=", StringComparison.OrdinalIgnoreCase))
            {
                var videoId = ExtractVideoIdFromUrl(url);
                if (string.IsNullOrEmpty(videoId))
                {
                    return LiveCheckResult.NotLive();
                }

                return await CheckWatchPageAsync(videoId, cancellationToken).ConfigureAwait(false);
            }

            var liveUrl = BuildLiveUrl(url);
            var (liveOk, liveHtml, liveError) = await FetchStringAsync(liveUrl, cancellationToken).ConfigureAwait(false);
            if (!liveOk)
            {
                return liveError ?? LiveCheckResult.Error("HTTP error");
            }
            var match = VideoIdRegex.Match(liveHtml ?? string.Empty);
            if (!match.Success)
            {
                return LiveCheckResult.NotLive();
            }

            var candidateId = match.Groups[1].Value;
            return await CheckWatchPageAsync(candidateId, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return LiveCheckResult.Error(ex.Message);
        }
    }

    private async Task<LiveCheckResult> CheckWatchPageAsync(string videoId, CancellationToken cancellationToken)
    {
        var watchUrl = $"https://www.youtube.com/watch?v={videoId}";
        var (watchOk, html, watchError) = await FetchStringAsync(watchUrl, cancellationToken).ConfigureAwait(false);
        if (!watchOk)
        {
            return watchError ?? LiveCheckResult.Error("HTTP error");
        }
        var liveMatch = IsLiveNowRegex.Match(html ?? string.Empty);
        if (liveMatch.Success && liveMatch.Groups[1].Value.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            return LiveCheckResult.Live(videoId);
        }

        return LiveCheckResult.NotLive();
    }

    private static string BuildLiveUrl(string url)
    {
        if (url.Contains("/live", StringComparison.OrdinalIgnoreCase))
        {
            return url;
        }

        url = url.TrimEnd('/');
        return url + "/live";
    }

    private static string? ExtractVideoIdFromUrl(string url)
    {
        var idx = url.IndexOf("watch?v=", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var part = url[(idx + "watch?v=".Length)..];
        var amp = part.IndexOf('&');
        if (amp >= 0)
        {
            part = part[..amp];
        }
        return part.Length == 11 ? part : null;
    }

    private async Task<(bool ok, string? content, LiveCheckResult? error)> FetchStringAsync(string url, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return (false, null, LiveCheckResult.Error($"HTTP {(int)response.StatusCode} {response.ReasonPhrase}"));
        }

        var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return (true, html, null);
    }
}

public readonly record struct LiveCheckResult(bool IsLive, string? VideoId, bool IsError, string? ErrorMessage)
{
    public static LiveCheckResult Live(string videoId) => new(true, videoId, false, null);
    public static LiveCheckResult NotLive() => new(false, null, false, null);
    public static LiveCheckResult Error(string message) => new(false, null, true, message);
}
