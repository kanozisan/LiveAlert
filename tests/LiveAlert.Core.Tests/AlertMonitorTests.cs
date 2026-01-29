using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using LiveAlert.Core;
using Xunit;

namespace LiveAlert.Core.Tests;

public sealed class AlertMonitorTests
{
    [Fact]
    public async Task AlertDetected_FiresOncePerVideoId()
    {
        var detector = new SequenceDetector(LiveCheckResult.Live("VID123"));
        var monitor = CreateMonitor(detector);
        var alert = new AlertConfig { Url = "https://www.youtube.com/@sample" };
        var options = new AlertOptions { PollIntervalSec = 60 };
        var detected = 0;
        monitor.AlertDetected += _ => detected++;

        await PollAsync(monitor, alert, 0, options);
        ForceNextAllowed(monitor, 0, DateTimeOffset.MinValue);
        await PollAsync(monitor, alert, 0, options);

        Assert.Equal(1, detected);
    }

    [Fact]
    public async Task AlertEnded_FiresWhenLiveEnds()
    {
        var detector = new SequenceDetector(
            LiveCheckResult.Live("VID999"),
            LiveCheckResult.NotLive());
        var monitor = CreateMonitor(detector);
        var alert = new AlertConfig { Url = "https://www.youtube.com/@sample" };
        var options = new AlertOptions { PollIntervalSec = 60 };
        var ended = 0;
        monitor.AlertEnded += _ => ended++;

        await PollAsync(monitor, alert, 0, options);
        ForceNextAllowed(monitor, 0, DateTimeOffset.MinValue);
        await PollAsync(monitor, alert, 0, options);

        Assert.Equal(1, ended);
    }

    [Fact]
    public async Task MonitoringFailure_FiresOnError()
    {
        var detector = new SequenceDetector(LiveCheckResult.Error("boom"));
        var monitor = CreateMonitor(detector);
        var alert = new AlertConfig { Url = "https://www.youtube.com/@sample", Label = "SAMPLE" };
        var options = new AlertOptions { PollIntervalSec = 60 };
        MonitoringFailure? failure = null;
        monitor.MonitoringFailureDetected += value => failure = value;

        var result = await PollAsync(monitor, alert, 0, options);

        Assert.True(result.isError);
        Assert.NotNull(failure);
        Assert.Equal("SAMPLE", failure!.Label);
    }

    [Fact]
    public async Task DebugMode_NotLiveForcesLiveEveryPoll()
    {
        var detector = new SequenceDetector(LiveCheckResult.NotLive());
        var monitor = CreateMonitor(detector);
        var alert = new AlertConfig { Url = "https://www.youtube.com/@sample" };
        var options = new AlertOptions { PollIntervalSec = 60, DebugMode = true };
        var detected = 0;
        monitor.AlertDetected += _ => detected++;

        await PollAsync(monitor, alert, 0, options);
        ForceNextAllowed(monitor, 0, DateTimeOffset.MinValue);
        await PollAsync(monitor, alert, 0, options);

        Assert.Equal(2, detected);
        Assert.Equal(0, GetNotifiedCount(monitor));
    }

    [Fact]
    public async Task DebugMode_DoesNotOverrideErrors()
    {
        var detector = new SequenceDetector(LiveCheckResult.Error("HTTP 500"));
        var monitor = CreateMonitor(detector);
        var alert = new AlertConfig { Url = "https://www.youtube.com/@sample" };
        var options = new AlertOptions { PollIntervalSec = 60, DebugMode = true };
        var detected = 0;
        monitor.AlertDetected += _ => detected++;

        await PollAsync(monitor, alert, 0, options);

        Assert.Equal(0, detected);
    }

    [Fact]
    public async Task NonYouTubeService_SkipsDetector()
    {
        var detector = new SequenceDetector(LiveCheckResult.Live("VID-A"));
        var monitor = CreateMonitor(detector);
        var alert = new AlertConfig { Url = "https://example.com", Service = "other" };
        var options = new AlertOptions { PollIntervalSec = 60 };

        await PollAsync(monitor, alert, 0, options);

        Assert.Equal(0, detector.CallCount);
    }

    private static AlertMonitor CreateMonitor(ILiveDetector detector)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "LiveAlertTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var configPath = Path.Combine(tempDir, "config.json");
        var manager = new ConfigManager(configPath);
        return new AlertMonitor(manager, detector);
    }

    private static async Task<(bool isError, bool isLive)> PollAsync(AlertMonitor monitor, AlertConfig alert, int index, AlertOptions options)
    {
        var method = typeof(AlertMonitor).GetMethod("PollAlertAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        var task = (Task)method!.Invoke(monitor, new object[] { alert, index, options, CancellationToken.None })!;
        await task.ConfigureAwait(false);
        var result = task.GetType().GetProperty("Result")!.GetValue(task);
        var isError = (bool)result!.GetType().GetProperty("IsError")!.GetValue(result)!;
        var isLive = (bool)result.GetType().GetProperty("IsLive")!.GetValue(result)!;
        return (isError, isLive);
    }

    private static void ForceNextAllowed(AlertMonitor monitor, int index, DateTimeOffset value)
    {
        var field = typeof(AlertMonitor).GetField("_state", BindingFlags.NonPublic | BindingFlags.Instance);
        var dict = (IDictionary)field!.GetValue(monitor)!;
        if (!dict.Contains(index))
        {
            return;
        }

        var state = dict[index]!;
        var prop = state.GetType().GetProperty("NextAllowed");
        prop!.SetValue(state, value);
    }

    private static int GetNotifiedCount(AlertMonitor monitor)
    {
        var field = typeof(AlertMonitor).GetField("_notifiedVideoIds", BindingFlags.NonPublic | BindingFlags.Instance);
        var set = (HashSet<string>)field!.GetValue(monitor)!;
        return set.Count;
    }

    private sealed class SequenceDetector : ILiveDetector
    {
        private readonly Queue<LiveCheckResult> _results = new();
        private LiveCheckResult _fallback = LiveCheckResult.NotLive();

        public int CallCount { get; private set; }

        public SequenceDetector(params LiveCheckResult[] results)
        {
            foreach (var result in results)
            {
                _results.Enqueue(result);
            }

            if (_results.Count > 0)
            {
                _fallback = results[^1];
            }
        }

        public Task<LiveCheckResult> CheckLiveAsync(AlertConfig alert, CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_results.Count > 0 ? _results.Dequeue() : _fallback);
        }
    }
}
