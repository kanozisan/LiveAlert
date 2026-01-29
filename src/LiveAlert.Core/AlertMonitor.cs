namespace LiveAlert.Core;

public sealed class AlertMonitor
{
    private readonly ConfigManager _configManager;
    private readonly ILiveDetector _youtube;
    private readonly Dictionary<int, AlertRuntimeState> _state = new();
    private readonly HashSet<string> _notifiedVideoIds = new();
    private static readonly bool EnableFailureBackoff = false;
    private static readonly bool EnableConfirmRecheck = false;

    public event Action<AlertEvent>? AlertDetected;
    public event Action<string>? AlertEnded;
    public event Action<MonitoringSummary>? MonitoringSummaryUpdated;
    public event Action<MonitoringFailure>? MonitoringFailureDetected;
    public event Action<string>? MonitoringDebug;

    public AlertMonitor(ConfigManager configManager, ILiveDetector youtube)
    {
        _configManager = configManager;
        _youtube = youtube;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await _configManager.LoadAsync(cancellationToken).ConfigureAwait(false);
            var config = _configManager.Current;
            var tasks = new List<Task>();
            var pollTasks = new List<Task<PollResult>>();
            for (var index = 0; index < config.Alerts.Count; index++)
            {
                var alert = config.Alerts[index];
                var pollTask = PollAlertAsync(alert, index, config.Options, cancellationToken);
                pollTasks.Add(pollTask);
                tasks.Add(pollTask);
            }

            await Task.WhenAll(tasks).ConfigureAwait(false);
            var results = pollTasks.Count == 0
                ? Array.Empty<PollResult>()
                : await Task.WhenAll(pollTasks).ConfigureAwait(false);
            var anyError = results.Any(result => result.IsError);
            var liveLabels = anyError
                ? Array.Empty<string>()
                : results.Where(result => result.IsLive)
                    .Select(result => result.Label)
                    .Where(label => !string.IsNullOrWhiteSpace(label))
                    .Cast<string>()
                    .ToArray();
            MonitoringSummaryUpdated?.Invoke(new MonitoringSummary(anyError, liveLabels));
            var delay = TimeSpan.FromSeconds(Math.Max(5, config.Options.PollIntervalSec));
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<PollResult> PollAlertAsync(AlertConfig alert, int index, AlertOptions options, CancellationToken cancellationToken)
    {
        if (!IsYouTube(alert))
        {
            return new PollResult(false, false, alert.Label);
        }

        var state = GetState(index);
        var now = DateTimeOffset.UtcNow;
        if (state.NextAllowed > now)
        {
            return new PollResult(false, !string.IsNullOrEmpty(state.CurrentLiveVideoId), alert.Label);
        }

        var result = await _youtube.CheckLiveAsync(alert, cancellationToken).ConfigureAwait(false);
        if (result.IsError)
        {
            state.FailureCount++;
            var nextDelay = EnableFailureBackoff
                ? GetBackoffDelay(options.PollIntervalSec, state.FailureCount)
                : TimeSpan.FromSeconds(Math.Max(5, options.PollIntervalSec));
            state.NextAllowed = now + nextDelay;
            MonitoringFailureDetected?.Invoke(new MonitoringFailure(alert.Label, alert.Url, result.ErrorMessage ?? string.Empty, state.FailureCount));
            return new PollResult(true, false, alert.Label);
        }

        state.FailureCount = 0;
        state.NextAllowed = now + TimeSpan.FromSeconds(Math.Max(5, options.PollIntervalSec));

        var debugForced = false;
        if (options.DebugMode && !result.IsLive)
        {
            var debugId = $"debug:{alert.Url.Trim()}";
            MonitoringDebug?.Invoke($"DebugMode forcing live label={alert.Label} videoId={debugId}");
            result = LiveCheckResult.Live(debugId);
            debugForced = true;
        }

        if (result.IsLive && !string.IsNullOrEmpty(result.VideoId))
        {
            var videoId = result.VideoId;
            if (!_notifiedVideoIds.Contains(videoId))
            {
                if (EnableConfirmRecheck)
                {
                    // Recheck once to reduce false positives
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
                    var confirm = await _youtube.CheckLiveAsync(alert, cancellationToken).ConfigureAwait(false);
                    if (confirm.IsLive && confirm.VideoId == videoId)
                    {
                        _notifiedVideoIds.Add(videoId);
                        state.CurrentLiveVideoId = videoId;
                        AlertDetected?.Invoke(new AlertEvent(alert, index, videoId, DateTimeOffset.UtcNow));
                        return new PollResult(false, true, alert.Label);
                    }
                    return new PollResult(false, false, alert.Label);
                }

                if (!debugForced)
                {
                    _notifiedVideoIds.Add(videoId);
                }
                state.CurrentLiveVideoId = videoId;
                AlertDetected?.Invoke(new AlertEvent(alert, index, videoId, DateTimeOffset.UtcNow));
                return new PollResult(false, true, alert.Label);
            }
            else
            {
                MonitoringDebug?.Invoke($"Already notified videoId={videoId} label={alert.Label}");
                state.CurrentLiveVideoId = videoId;
            }
        }
        else
        {
            if (!string.IsNullOrEmpty(state.CurrentLiveVideoId))
            {
                AlertEnded?.Invoke(state.CurrentLiveVideoId);
                state.CurrentLiveVideoId = null;
            }
        }

        return new PollResult(false, !string.IsNullOrEmpty(state.CurrentLiveVideoId), alert.Label);
    }

    private AlertRuntimeState GetState(int index)
    {
        if (!_state.TryGetValue(index, out var state))
        {
            state = new AlertRuntimeState();
            _state[index] = state;
        }

        return state;
    }

    private static bool IsYouTube(AlertConfig alert)
    {
        return string.IsNullOrWhiteSpace(alert.Service) ||
               alert.Service.Equals("youtube", StringComparison.OrdinalIgnoreCase);
    }

    private static TimeSpan GetBackoffDelay(int baseSeconds, int failures)
    {
        var exp = Math.Min(5, failures); // cap
        var delay = baseSeconds * Math.Pow(2, exp);
        return TimeSpan.FromSeconds(Math.Max(baseSeconds, delay));
    }
}

public sealed record AlertEvent(AlertConfig Alert, int AlertIndex, string VideoId, DateTimeOffset DetectedAt);
public sealed record MonitoringSummary(bool AnyError, IReadOnlyList<string> LiveLabels);
public sealed record MonitoringFailure(string Label, string Url, string Reason, int FailureCount);

internal sealed record PollResult(bool IsError, bool IsLive, string Label);

internal sealed class AlertRuntimeState
{
    public int FailureCount { get; set; }
    public DateTimeOffset NextAllowed { get; set; }
    public string? CurrentLiveVideoId { get; set; }
    public bool WarningActive { get; set; }
}
