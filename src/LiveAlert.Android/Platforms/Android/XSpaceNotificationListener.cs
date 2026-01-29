using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Service.Notification;
using AndroidApp = Android.App.Application;
using LiveAlert.Core;
using AndroidX.Core.Content;

namespace LiveAlert;

[Service(
    Name = "com.yasagurebug.livealert.XSpaceNotificationListener",
    Permission = "android.permission.BIND_NOTIFICATION_LISTENER_SERVICE",
    Exported = true)]
[IntentFilter(new[] { "android.service.notification.NotificationListenerService" })]
public sealed class XSpaceNotificationListener : NotificationListenerService
{
    private const string TwitterPackageName = "com.twitter.android";
    private static readonly object DedupeLock = new();
    private static readonly Dictionary<string, DateTimeOffset> DedupeKeys = new();
    private static readonly Regex SpaceUrlRegex = new(@"https?://(x\.com|twitter\.com)/i/spaces/\S+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public override void OnNotificationPosted(StatusBarNotification? sbn)
    {
        if (sbn == null) return;
        if (!string.Equals(sbn.PackageName, TwitterPackageName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var notification = sbn.Notification;
        if (notification == null) return;

        var extras = notification.Extras;
        if (extras == null) return;

        var title = GetText(extras, Notification.ExtraTitle);
        var text = GetText(extras, Notification.ExtraText);
        var bigText = GetText(extras, Notification.ExtraBigText);
        var body = BuildBody(text, bigText);
        if (!IsSpace(body))
        {
            return;
        }

        _ = HandleSpaceNotificationAsync(sbn, notification, title, body);
    }

    private static async Task HandleSpaceNotificationAsync(StatusBarNotification sbn, Notification notification, string title, string body)
    {
        try
        {
            var configPath = System.IO.Path.Combine(AndroidApp.Context.FilesDir?.AbsolutePath ?? ".", "config.json");
            var manager = new ConfigManager(configPath);
            await manager.LoadAsync().ConfigureAwait(false);
            var config = manager.Current;
            var dedupeMinutes = Math.Clamp(config.DedupeMinutes <= 0 ? 5 : config.DedupeMinutes, 1, 30);

            var dedupeKey = BuildDedupeKey(sbn);
            if (IsDuplicate(dedupeKey, dedupeMinutes))
            {
                AppLog.Info($"XSpace dedupe skipped key={dedupeKey}");
                return;
            }

            var matches = config.Alerts
                .Select((alert, index) => new { Alert = alert, Index = index })
                .Where(item => IsXSpace(item.Alert))
                .Where(item => TitleMatches(title, item.Alert.TitleContains))
                .ToList();

            if (matches.Count == 0)
            {
                AppLog.Info($"XSpace no match title='{title}'");
                return;
            }

            var contentIntent = notification.ContentIntent;
            var url = ExtractSpaceUrl(body);

            foreach (var match in matches)
            {
                SendToForegroundService(match.Index, match.Alert.Label, dedupeKey, contentIntent, url);
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn($"XSpaceNotificationListener failed: {ex.Message}");
        }
    }

    private static void SendToForegroundService(int alertIndex, string label, string videoId, PendingIntent? contentIntent, string? url)
    {
        var context = AndroidApp.Context;
        var intent = new Intent(context, typeof(LiveAlertForegroundService));
        intent.SetAction(ServiceController.ActionXSpaceDetected);
        intent.PutExtra(ServiceController.ExtraAlertIndex, alertIndex);
        intent.PutExtra(ServiceController.ExtraVideoId, videoId);
        intent.PutExtra(ServiceController.ExtraLabel, label ?? string.Empty);
        if (!string.IsNullOrWhiteSpace(url))
        {
            intent.PutExtra(ServiceController.ExtraTargetUrl, url);
        }
        if (contentIntent != null)
        {
            intent.PutExtra(ServiceController.ExtraContentIntent, contentIntent);
        }

        ContextCompat.StartForegroundService(context, intent);
    }

    private static bool IsXSpace(AlertConfig alert)
    {
        return string.Equals(alert.Service?.Trim(), "x_space", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TitleMatches(string title, string match)
    {
        var titleValue = title?.Trim() ?? string.Empty;
        var matchValue = match?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(titleValue) || string.IsNullOrEmpty(matchValue))
        {
            return false;
        }

        return titleValue.IndexOf(matchValue, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsSpace(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return false;
        return body.Contains("x.com/i/spaces", StringComparison.OrdinalIgnoreCase)
            || body.Contains("twitter.com/i/spaces", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetText(Bundle extras, string key)
    {
        try
        {
            return extras.GetCharSequence(key)?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string BuildBody(string text, string bigText)
    {
        if (string.IsNullOrWhiteSpace(text)) return bigText ?? string.Empty;
        if (string.IsNullOrWhiteSpace(bigText)) return text;
        return $"{text}\n{bigText}";
    }

    private static string BuildDedupeKey(StatusBarNotification sbn)
    {
        var key = sbn.Key;
        if (!string.IsNullOrWhiteSpace(key)) return key;
        return $"{sbn.PackageName}:{sbn.Id}:{sbn.PostTime}";
    }

    private static bool IsDuplicate(string key, int dedupeMinutes)
    {
        var now = DateTimeOffset.UtcNow;
        var cutoff = now.AddMinutes(-dedupeMinutes);
        lock (DedupeLock)
        {
            var expired = DedupeKeys.Where(pair => pair.Value < cutoff).Select(pair => pair.Key).ToList();
            foreach (var item in expired)
            {
                DedupeKeys.Remove(item);
            }

            if (DedupeKeys.ContainsKey(key))
            {
                return true;
            }
            DedupeKeys[key] = now;
            return false;
        }
    }

    private static string? ExtractSpaceUrl(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        var match = SpaceUrlRegex.Match(body);
        if (match.Success)
        {
            var url = match.Value.Trim();
            if (url.Contains("…") || url.Contains("..."))
            {
                return null;
            }
            return url;
        }

        return null;
    }
}
