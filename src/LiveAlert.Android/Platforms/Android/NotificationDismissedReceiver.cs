using Android.App;
using Android.Content;

namespace LiveAlert;

[BroadcastReceiver(Enabled = true, Exported = false)]
[IntentFilter(new[] { ActionNotificationDismissed })]
public sealed class NotificationDismissedReceiver : BroadcastReceiver
{
    public const string ActionNotificationDismissed = "livealert.action.NOTIFICATION_DISMISSED";

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action != ActionNotificationDismissed)
        {
            return;
        }

        AppLog.Info("NotificationDismissedReceiver.OnReceive");
        if (!ServiceController.AutoRestartEnabled)
        {
            AppLog.Info("NotificationDismissedReceiver ignored: auto restart disabled");
            return;
        }
        ServiceController.RefreshNotification();
    }
}
