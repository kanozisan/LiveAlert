using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Media;
using Android.OS;
using AndroidX.Core.App;
using LiveAlert.Core;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace LiveAlert;

[Service(
    Name = "com.yasagurebug.livealert.LiveAlertForegroundService",
    Exported = false,
    ForegroundServiceType = ForegroundService.TypeMediaPlayback | ForegroundService.TypeDataSync)]
public class LiveAlertForegroundService : Service
{
    private const int ForegroundId = 1001;
    private const string ForegroundChannelId = "livealert.monitor";
    private const string AlertChannelId = "livealert.alerts";

    private readonly object _lock = new();
    private readonly Handler _mainHandler = new(Looper.MainLooper!);
    private string _monitorStatusText = "監視中";
    private bool _showOverlayAfterUnlock;
    private BroadcastReceiver? _userPresentReceiver;

    private ConfigManager? _configManager;
    private AlertMonitor? _monitor;
    private CancellationTokenSource? _monitorCts;
    private Task? _monitorTask;

    private AlertQueue _queue = new();
    private AlertQueueItem? _current;
    private readonly Dictionary<string, PendingIntent?> _contentIntents = new();
    private readonly Dictionary<string, string?> _contentUrls = new();
    private CancellationTokenSource? _alertCts;
    private AlertOverlay? _overlay;
    private AlertAudioPlayer? _audio;
    private PowerManager.WakeLock? _wakeLock;
    private bool _foregroundStarted;

    public override void OnCreate()
    {
        base.OnCreate();
        AppLog.Info("LiveAlertForegroundService.OnCreate");

        var services = Microsoft.Maui.IPlatformApplication.Current?.Services
            ?? throw new InvalidOperationException("IPlatformApplication.Current.Services is not available.");
        _configManager = services.GetRequiredService<ConfigManager>();
        _monitor = services.GetRequiredService<AlertMonitor>();
        _overlay = new AlertOverlay(this);
        _audio = new AlertAudioPlayer(this);
        _overlay.Tapped += HandleOverlayTapped;

        _monitor.AlertDetected += OnAlertDetected;
        _monitor.AlertEnded += OnAlertEnded;
        _monitor.MonitoringSummaryUpdated += OnMonitoringSummaryUpdated;
        _monitor.MonitoringFailureDetected += OnMonitoringFailureDetected;
        _monitor.MonitoringDebug += OnMonitoringDebug;

        CreateNotificationChannels();
        RegisterUserPresentReceiver();
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        var action = intent?.Action ?? ServiceController.ActionStart;
        AppLog.Info($"OnStartCommand action={action} flags={flags} startId={startId}");
        if (action == ServiceController.ActionTest)
        {
            EnsureForeground();
        }
        switch (action)
        {
            case ServiceController.ActionStart:
                StartMonitoring();
                break;
            case ServiceController.ActionStop:
                StopSelf();
                break;
            case ServiceController.ActionTest:
                TriggerTestAlert();
                break;
            case ServiceController.ActionStopAlert:
                StopCurrentAlert();
                break;
            case ServiceController.ActionRefreshNotification:
                EnsureForeground();
                UpdateMonitorNotification(_monitorStatusText);
                break;
            case ServiceController.ActionXSpaceDetected:
                EnsureForeground();
                HandleXSpaceDetected(intent);
                break;
        }

        return StartCommandResult.Sticky;
    }

    public override void OnDestroy()
    {
        base.OnDestroy();
        AppLog.Info("LiveAlertForegroundService.OnDestroy");
        _monitorCts?.Cancel();
        _monitorCts = null;
        _foregroundStarted = false;

        if (_monitor != null)
        {
            _monitor.AlertDetected -= OnAlertDetected;
            _monitor.AlertEnded -= OnAlertEnded;
            _monitor.MonitoringSummaryUpdated -= OnMonitoringSummaryUpdated;
            _monitor.MonitoringFailureDetected -= OnMonitoringFailureDetected;
            _monitor.MonitoringDebug -= OnMonitoringDebug;
        }

        _mainHandler.Post(() =>
        {
            _overlay?.Hide();
            _audio?.Stop();
        });

        ReleaseWakeLock();
        UnregisterUserPresentReceiver();
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
        {
            StopForeground(StopForegroundFlags.Remove);
        }
        else
        {
#pragma warning disable CA1422
            StopForeground(true);
#pragma warning restore CA1422
        }
    }

    public override IBinder? OnBind(Intent? intent) => null;

    private void StartMonitoring()
    {
        if (_monitorCts != null) return;
        if (_configManager == null || _monitor == null) return;

        _monitorCts = new CancellationTokenSource();
        EnsureForeground();
        AppLog.Info("StartMonitoring");

        _monitorTask = Task.Run(async () =>
        {
            try
            {
                await _configManager.LoadAsync(_monitorCts.Token).ConfigureAwait(false);
                await _monitor.RunAsync(_monitorCts.Token).ConfigureAwait(false);
            }
            catch (System.OperationCanceledException)
            {
            }
        });
    }

    private void EnsureForeground()
    {
        if (_foregroundStarted) return;
        AppLog.Info("EnsureForeground");
        var notification = BuildForegroundNotification();
        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            StartForeground(ForegroundId, notification, ForegroundService.TypeMediaPlayback | ForegroundService.TypeDataSync);
        }
        else
        {
            StartForeground(ForegroundId, notification);
        }
        _foregroundStarted = true;
    }

    private void TriggerTestAlert()
    {
        if (_configManager == null) return;
        var config = _configManager.Current;
        var alert = config.Alerts.FirstOrDefault() ?? new AlertConfig
        {
            Label = "TEST",
            Message = "警告　{label} がライブ開始",
            Colors = new AlertColors()
        };

        AppLog.Info("TriggerTestAlert");
        lock (_lock)
        {
            if (_current != null)
            {
                AppLog.Info("TriggerTestAlert stopping active alert");
                StopCurrentAlertLocked(startNext: false);
                _queue = new AlertQueue();
            }
        }

        var index = config.Alerts.IndexOf(alert);
        var ev = new AlertEvent(alert, index < 0 ? 0 : index, "test-video", DateTimeOffset.UtcNow);
        OnAlertDetected(ev);
    }

    private void OnAlertDetected(AlertEvent alertEvent)
    {
        lock (_lock)
        {
            if (_current != null && _current.VideoId == alertEvent.VideoId) return;
            if (_queue.Contains(alertEvent.VideoId)) return;

            AppLog.Info($"AlertDetected index={alertEvent.AlertIndex} video={alertEvent.VideoId}");
            _queue.Enqueue(new AlertQueueItem(alertEvent, alertEvent.DetectedAt));

            if (_current == null)
            {
                StartNextAlertLocked();
            }
        }
    }

    private void HandleXSpaceDetected(Intent? intent)
    {
        if (intent == null || _configManager == null) return;

        var index = intent.GetIntExtra(ServiceController.ExtraAlertIndex, -1);
        var videoId = intent.GetStringExtra(ServiceController.ExtraVideoId) ?? string.Empty;
        if (index < 0 || string.IsNullOrWhiteSpace(videoId))
        {
            return;
        }

        _configManager.LoadAsync().GetAwaiter().GetResult();

        if (index >= _configManager.Current.Alerts.Count)
        {
            return;
        }

        var alert = _configManager.Current.Alerts[index];
        PendingIntent? pending;
#pragma warning disable CA1416, CA1422
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
        {
            pending = intent.GetParcelableExtra(ServiceController.ExtraContentIntent, Java.Lang.Class.FromType(typeof(PendingIntent))) as PendingIntent;
        }
        else
        {
            pending = intent.GetParcelableExtra(ServiceController.ExtraContentIntent) as PendingIntent;
        }
#pragma warning restore CA1416, CA1422
        var url = intent.GetStringExtra(ServiceController.ExtraTargetUrl);
        var label = intent.GetStringExtra(ServiceController.ExtraLabel) ?? alert.Label;

        lock (_lock)
        {
            _contentIntents[videoId] = pending;
            if (!string.IsNullOrWhiteSpace(url))
            {
                _contentUrls[videoId] = url;
            }
        }

        UpdateMonitorNotificationForPush(label);

        var ev = new AlertEvent(alert, index, videoId, DateTimeOffset.UtcNow);
        OnAlertDetected(ev);
    }

    private void OnAlertEnded(string videoId)
    {
        lock (_lock)
        {
            AppLog.Info($"AlertEnded video={videoId}");
            _queue.RemoveByVideoId(videoId);
            if (_current != null && _current.VideoId == videoId)
            {
                StopCurrentAlertLocked();
            }
        }
    }

    private void OnMonitoringFailureDetected(MonitoringFailure failure)
    {
        var label = string.IsNullOrWhiteSpace(failure.Label) ? "(no label)" : failure.Label;
        var url = string.IsNullOrWhiteSpace(failure.Url) ? "(no url)" : failure.Url;
        var reason = string.IsNullOrWhiteSpace(failure.Reason) ? "(no reason)" : failure.Reason;
        AppLog.Warn($"Monitor failure label={label} url={url} count={failure.FailureCount} reason={reason}");
    }

    private void OnMonitoringSummaryUpdated(MonitoringSummary summary)
    {
        var now = DateTime.Now;
        string text;
        if (summary.AnyError)
        {
            text = $"{now:HH:mm} 監視に失敗しました";
        }
        else if (summary.LiveLabels.Count > 0)
        {
            var labels = string.Join(",", summary.LiveLabels);
            text = $"{now:HH:mm} 監視：{labels}でLIVE検知";
        }
        else
        {
            text = $"{now:HH:mm} 監視：LIVEなし";
        }

        _mainHandler.Post(() => UpdateMonitorNotification(text));
    }

    private void OnMonitoringDebug(string message)
    {
        AppLog.Info($"Monitor debug: {message}");
    }

    private void HandleOverlayTapped()
    {
        PendingIntent? contentIntent = null;
        string? targetUrl = null;
        lock (_lock)
        {
            if (_current != null)
            {
                contentIntent = GetContentIntent(_current);
                targetUrl = BuildAlertUrl(_current);
            }
        }

        StopCurrentAlert();

        if (contentIntent != null)
        {
            try
            {
                contentIntent.Send();
                return;
            }
            catch (Exception ex)
            {
                AppLog.Warn($"Overlay.ContentIntent failed error={ex.Message}");
            }
        }

        if (!string.IsNullOrWhiteSpace(targetUrl))
        {
            try
            {
                var intent = new Intent(Intent.ActionView, Android.Net.Uri.Parse(targetUrl));
                intent.AddFlags(ActivityFlags.NewTask);
                StartActivity(intent);
            }
            catch (Exception ex)
            {
                AppLog.Warn($"Overlay.OpenUrl failed url={targetUrl} error={ex.Message}");
            }
        }
    }

    private string? BuildAlertUrl(AlertQueueItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.VideoId))
        {
            if (_contentUrls.TryGetValue(item.VideoId, out var url) && !string.IsNullOrWhiteSpace(url))
            {
                return url;
            }

            if (item.VideoId.Length == 11)
            {
                return $"https://www.youtube.com/watch?v={item.VideoId}";
            }
        }

        var trimmed = item.Alert.Url.Trim();
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }

    private static string GetDefaultVoiceAsset(AlertConfig alert)
    {
        return string.Equals(alert.Service?.Trim(), "x_space", StringComparison.OrdinalIgnoreCase)
            ? AlertAudioPlayer.DefaultVoiceSpaceAsset
            : AlertAudioPlayer.DefaultVoiceLiveAsset;
    }

    private void StartNextAlertLocked()
    {
        var next = _queue.DequeueNext();
        if (next == null) return;
        _current = next;
        _alertCts?.Cancel();
        _alertCts = null;

        var options = _configManager?.Current.Options ?? new AlertOptions();
        var message = next.Alert.Message.Replace("{label}", next.Alert.Label ?? string.Empty);
        var notificationMode = NormalizeMode(options.NotificationMode, "alarm");
        var displayMode = NormalizeMode(options.DisplayMode, "alarm");
        var audioMode = NormalizeMode(options.AudioMode, "alarm");
        var locked = IsLockedOrScreenOff();
        var useFullscreen = displayMode == "alarm" && locked;
        _showOverlayAfterUnlock = useFullscreen;
        AppLog.Info($"StartAlert label={next.Alert.Label} video={next.VideoId} notify={notificationMode} display={displayMode} audio={audioMode} locked={locked}");

        _mainHandler.Post(() =>
        {
            if (useFullscreen)
            {
                _overlay?.Hide();
            }
            else if (ShouldShowOverlay(displayMode))
            {
                _overlay?.Show(message, options, next.Alert.Colors, displayMode == "alarm");
            }
            else
            {
                _overlay?.Hide();
            }

            var usage = audioMode == "alarm" ? AudioUsageKind.Alarm : AudioUsageKind.Media;
            var audioEnabled = audioMode != "off";
            var voicePath = string.IsNullOrWhiteSpace(next.Alert.Voice)
                ? AlertAudioPlayer.BuildAssetSource(GetDefaultVoiceAsset(next.Alert))
                : next.Alert.Voice;
            _audio?.Start(voicePath, next.Alert.VoiceVolume, next.Alert.Bgm, next.Alert.BgmVolume, options.LoopIntervalSec, options.MaxAlarmDurationSec, usage, audioEnabled);
        });

        if (displayMode == "alarm")
        {
            AcquireWakeLock();
        }

        var alertColors = next.Alert.Colors ?? new AlertColors();
        var contentPending = ResolveContentIntent(next, 4);
        var fallbackUrl = BuildAlertUrl(next);
        if (notificationMode == "alarm")
        {
            ShowAlarmNotification(message, contentPending ?? CreateAlertContentIntent(fallbackUrl, 5), alertColors, useFullscreen);
        }
        else if (notificationMode == "manner")
        {
            ShowAlertNotification(message, vibrate: true, contentPending ?? CreateAlertContentIntent(fallbackUrl, 4), alertColors, useFullscreen);
        }
        else if (useFullscreen)
        {
            ShowAlarmNotification(message, contentPending ?? CreateAlertContentIntent(fallbackUrl, 5), alertColors, useFullscreen);
        }

        if (options.MaxAlarmDurationSec > 0)
        {
            var cts = new CancellationTokenSource();
            _alertCts = cts;
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(options.MaxAlarmDurationSec), cts.Token).ConfigureAwait(false);
                    StopCurrentAlert();
                }
                catch (System.OperationCanceledException)
                {
                }
            });
        }
    }

    private void StopCurrentAlert()
    {
        lock (_lock)
        {
            StopCurrentAlertLocked();
        }
    }

    private void StopCurrentAlertLocked(bool startNext = true)
    {
        var previous = _current;
        if (previous != null)
        {
            AppLog.Info($"StopAlert video={previous.VideoId}");
        }
        _current = null;
        _alertCts?.Cancel();
        _alertCts = null;
        _showOverlayAfterUnlock = false;
        DismissFullscreenActivity();

        if (previous != null)
        {
            _contentIntents.Remove(previous.VideoId);
            _contentUrls.Remove(previous.VideoId);
        }

        _mainHandler.Post(() =>
        {
            _overlay?.Hide();
            _audio?.Stop();
        });

        ReleaseWakeLock();
        if (startNext)
        {
            StartNextAlertLocked();
        }
    }

    private Notification BuildForegroundNotification()
    {
        var stopIntent = new Intent(this, typeof(LiveAlertForegroundService));
        stopIntent.SetAction(ServiceController.ActionStopAlert);
        var stopPending = PendingIntent.GetService(this, 1, stopIntent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var contentIntent = new Intent(this, typeof(MainActivity));
        contentIntent.AddFlags(ActivityFlags.SingleTop | ActivityFlags.ClearTop);
        var contentPending = PendingIntent.GetActivity(this, 0, contentIntent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var dismissIntent = new Intent(this, typeof(NotificationDismissedReceiver));
        dismissIntent.SetAction(NotificationDismissedReceiver.ActionNotificationDismissed);
        var dismissPending = PendingIntent.GetBroadcast(this, 3, dismissIntent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);

        var builder = new NotificationCompat.Builder(this, ForegroundChannelId)
            .SetContentTitle("LiveAlert")
            .SetContentText(_monitorStatusText)
            .SetSmallIcon(Resource.Drawable.ic_stat_livealert)
            .SetOngoing(true)
            .SetContentIntent(contentPending)
            .SetDeleteIntent(dismissPending);

        if (stopPending != null)
        {
            builder.AddAction(Android.Resource.Drawable.IcMediaPause, "アラーム停止", stopPending);
        }

        return builder.Build();
    }

    private void UpdateMonitorNotification(string text)
    {
        _monitorStatusText = text;
        var notification = BuildForegroundNotification();
        NotificationManagerCompat.From(this).Notify(ForegroundId, notification);
    }

    private void ShowAlertNotification(string message, bool vibrate, PendingIntent? contentPending, AlertColors alertColors, bool useFullscreen)
    {
        var stopIntent = new Intent(this, typeof(LiveAlertForegroundService));
        stopIntent.SetAction(ServiceController.ActionStopAlert);
        var stopPending = PendingIntent.GetService(this, 2, stopIntent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        var fullscreenPending = useFullscreen ? CreateAlertFullscreenIntent(message, alertColors, 6) : null;

        var builder = new NotificationCompat.Builder(this, AlertChannelId)
            .SetContentTitle("LiveAlert")
            .SetContentText(message)
            .SetSmallIcon(Resource.Drawable.ic_stat_livealert)
            .SetPriority((int)NotificationPriority.High)
            .SetAutoCancel(true);
        if (contentPending != null)
        {
            builder.SetContentIntent(contentPending);
        }
        if (fullscreenPending != null)
        {
            builder.SetFullScreenIntent(fullscreenPending, true);
        }

        if (vibrate)
        {
            builder.SetDefaults((int)NotificationDefaults.Vibrate);
        }

        if (stopPending != null)
        {
            builder.AddAction(Android.Resource.Drawable.IcMediaPause, "アラーム停止", stopPending);
        }

        NotificationManagerCompat.From(this).Notify(2001, builder.Build());
    }

    private void ShowAlarmNotification(string message, PendingIntent? contentPending, AlertColors alertColors, bool useFullscreen)
    {
        var fullscreenPending = useFullscreen ? CreateAlertFullscreenIntent(message, alertColors, 7) : null;
        var builder = new NotificationCompat.Builder(this, AlertChannelId)
            .SetContentTitle("LiveAlert ALARM")
            .SetContentText(message)
            .SetSmallIcon(Resource.Drawable.ic_stat_livealert)
            .SetPriority((int)NotificationPriority.Max)
            .SetCategory(Notification.CategoryAlarm)
            .SetAutoCancel(true);
        if (contentPending != null)
        {
            builder.SetContentIntent(contentPending);
        }
        if (fullscreenPending != null)
        {
            builder.SetFullScreenIntent(fullscreenPending, true);
        }

        NotificationManagerCompat.From(this).Notify(2002, builder.Build());
    }

    private PendingIntent? CreateAlertContentIntent(string? targetUrl, int requestCode)
    {
        if (string.IsNullOrWhiteSpace(targetUrl))
        {
            return null;
        }

        try
        {
            var intent = new Intent(Intent.ActionView, Android.Net.Uri.Parse(targetUrl));
            intent.AddFlags(ActivityFlags.NewTask);
            return PendingIntent.GetActivity(this, requestCode, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        }
        catch (Exception ex)
        {
            AppLog.Warn($"AlertNotification content intent failed url={targetUrl} error={ex.Message}");
            return null;
        }
    }

    private PendingIntent? GetContentIntent(AlertQueueItem item)
    {
        if (!string.IsNullOrWhiteSpace(item.VideoId) && _contentIntents.TryGetValue(item.VideoId, out var pending))
        {
            return pending;
        }

        return null;
    }

    private PendingIntent? ResolveContentIntent(AlertQueueItem item, int requestCode)
    {
        var pending = GetContentIntent(item);
        if (pending != null) return pending;
        var url = BuildAlertUrl(item);
        return CreateAlertContentIntent(url, requestCode);
    }

    private void UpdateMonitorNotificationForPush(string label)
    {
        var safeLabel = string.IsNullOrWhiteSpace(label) ? "(no label)" : label;
        var now = DateTime.Now;
        var text = $"{now:HH:mm} 監視：{safeLabel}でLIVE検知";
        _mainHandler.Post(() => UpdateMonitorNotification(text));
    }

    private PendingIntent? CreateAlertFullscreenIntent(string message, AlertColors alertColors, int requestCode)
    {
        try
        {
            var intent = new Intent(this, typeof(AlertFullscreenActivity));
            intent.AddFlags(ActivityFlags.NewTask | ActivityFlags.SingleTop | ActivityFlags.ClearTop);
            intent.PutExtra(AlertFullscreenActivity.ExtraMessage, message);
            intent.PutExtra(AlertFullscreenActivity.ExtraBackground, alertColors.Background ?? "#FF0000");
            intent.PutExtra(AlertFullscreenActivity.ExtraText, alertColors.Text ?? "#000000");
            return PendingIntent.GetActivity(this, requestCode, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        }
        catch (Exception ex)
        {
            AppLog.Warn($"AlertNotification fullscreen intent failed error={ex.Message}");
            return null;
        }
    }

    private void CreateNotificationChannels()
    {
        if (Build.VERSION.SdkInt < BuildVersionCodes.O) return;
        var manager = (NotificationManager)GetSystemService(NotificationService)!;

        var monitorChannel = new NotificationChannel(ForegroundChannelId, "LiveAlert Monitoring", NotificationImportance.Low)
        {
            Description = "LiveAlert foreground service"
        };
        manager.CreateNotificationChannel(monitorChannel);

        var alertChannel = new NotificationChannel(AlertChannelId, "LiveAlert Alerts", NotificationImportance.High)
        {
            Description = "LiveAlert alerts"
        };
        manager.CreateNotificationChannel(alertChannel);
    }

    private void RegisterUserPresentReceiver()
    {
        if (_userPresentReceiver != null) return;
        _userPresentReceiver = new UserPresentReceiver(this);
        RegisterReceiver(_userPresentReceiver, new IntentFilter(Intent.ActionUserPresent));
    }

    private void UnregisterUserPresentReceiver()
    {
        if (_userPresentReceiver == null) return;
        UnregisterReceiver(_userPresentReceiver);
        _userPresentReceiver = null;
    }

    private void HandleUserPresent()
    {
        AlertQueueItem? current;
        AlertOptions options;
        lock (_lock)
        {
            if (!_showOverlayAfterUnlock) return;
            current = _current;
            options = _configManager?.Current.Options ?? new AlertOptions();
            _showOverlayAfterUnlock = false;
        }

        if (current == null) return;
        var displayMode = NormalizeMode(options.DisplayMode, "alarm");
        if (displayMode != "alarm") return;

        var message = current.Alert.Message.Replace("{label}", current.Alert.Label ?? string.Empty);
        _mainHandler.Post(() =>
        {
            _overlay?.Show(message, options, current.Alert.Colors, true);
        });
    }

    private void DismissFullscreenActivity()
    {
        try
        {
            var intent = new Intent(AlertFullscreenActivity.ActionDismiss);
            SendBroadcast(intent);
        }
        catch
        {
        }
    }

    private bool IsLockedOrScreenOff()
    {
        var powerManager = (PowerManager)GetSystemService(PowerService)!;
        if (powerManager == null || !powerManager.IsInteractive) return true;

        var keyguard = (KeyguardManager)GetSystemService(KeyguardService)!;
        return keyguard != null && keyguard.IsKeyguardLocked;
    }

    private sealed class UserPresentReceiver : BroadcastReceiver
    {
        private readonly LiveAlertForegroundService _service;

        public UserPresentReceiver(LiveAlertForegroundService service)
        {
            _service = service;
        }

        public override void OnReceive(Context? context, Intent? intent)
        {
            _service.HandleUserPresent();
        }
    }

    private bool ShouldShowOverlay(string displayMode)
    {
        if (displayMode == "off") return false;
        if (displayMode == "alarm") return true;

        var powerManager = (PowerManager)GetSystemService(PowerService)!;
        if (powerManager == null || !powerManager.IsInteractive) return false;

        var keyguard = (KeyguardManager)GetSystemService(KeyguardService)!;
        if (keyguard != null && keyguard.IsKeyguardLocked) return false;

        return true;
    }

    private static string NormalizeMode(string? mode, string fallback)
    {
        return mode switch
        {
            "alarm" => "alarm",
            "manner" => "manner",
            "off" => "off",
            _ => fallback
        };
    }

    private void AcquireWakeLock()
    {
        if (_wakeLock?.IsHeld == true) return;
        var powerManager = (PowerManager)GetSystemService(PowerService)!;
        _wakeLock = powerManager.NewWakeLock(
            WakeLockFlags.ScreenBright | WakeLockFlags.AcquireCausesWakeup | WakeLockFlags.OnAfterRelease,
            "LiveAlert:WakeLock");
        _wakeLock?.Acquire(10 * 60 * 1000L);
    }

    private void ReleaseWakeLock()
    {
        if (_wakeLock == null) return;
        try
        {
            if (_wakeLock.IsHeld)
            {
                _wakeLock.Release();
            }
        }
        catch
        {
        }
        finally
        {
            _wakeLock = null;
        }
    }
}
