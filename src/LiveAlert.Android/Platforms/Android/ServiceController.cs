using System.Linq;
using Android.Content;
using Android.OS;
using AndroidX.Core.Content;
using AndroidApp = Android.App.Application;
using JavaClass = Java.Lang.Class;

namespace LiveAlert;

public static class ServiceController
{
    public const string ActionStart = "livealert.action.START";
    public const string ActionStop = "livealert.action.STOP";
    public const string ActionTest = "livealert.action.TEST";
    public const string ActionStopAlert = "livealert.action.STOP_ALERT";
    public const string ActionRefreshNotification = "livealert.action.REFRESH_NOTIFICATION";
    public const string ActionXSpaceDetected = "livealert.action.XSPACE_DETECTED";

    public const string ExtraAlertIndex = "alertIndex";
    public const string ExtraVideoId = "videoId";
    public const string ExtraLabel = "label";
    public const string ExtraContentIntent = "contentIntent";
    public const string ExtraTargetUrl = "targetUrl";

    private static bool _warningActive;
    private static string _warningMessage = string.Empty;
    private static bool _autoRestartEnabled;
    private static bool _debugMode;

    public static bool WarningActive => _warningActive;
    public static string WarningMessage => _warningMessage;
    public static bool AutoRestartEnabled => _autoRestartEnabled;
    public static bool DebugMode => _debugMode;

    public static event Action<bool, string>? WarningChanged;
    public static event Action<bool>? DebugModeChanged;

    public static bool IsRunning()
    {
        var context = AndroidApp.Context;
        var manager = context.GetSystemService(Context.ActivityService) as Android.App.ActivityManager;
        if (manager == null) return false;

        var className = JavaClass.FromType(typeof(LiveAlertForegroundService)).Name;
#pragma warning disable CA1416
        #pragma warning disable CA1422
        var services = manager.GetRunningServices(int.MaxValue);
        #pragma warning restore CA1422
        return services != null && services.Any(service => service?.Service?.ClassName == className);
#pragma warning restore CA1416
    }

    public static void Start()
    {
        var context = AndroidApp.Context;
        var intent = new Intent(context, JavaClass.FromType(typeof(LiveAlertForegroundService)));
        intent.SetAction(ActionStart);
        AppLog.Info("ServiceController.Start");
        _autoRestartEnabled = true;
        ContextCompat.StartForegroundService(context, intent);
    }

    public static void Stop()
    {
        var context = AndroidApp.Context;
        var intent = new Intent(context, JavaClass.FromType(typeof(LiveAlertForegroundService)));
        intent.SetAction(ActionStop);
        AppLog.Info("ServiceController.Stop");
        _autoRestartEnabled = false;
        context.StartService(intent);
    }

    public static void TriggerTestAlert()
    {
        var context = AndroidApp.Context;
        var intent = new Intent(context, JavaClass.FromType(typeof(LiveAlertForegroundService)));
        intent.SetAction(ActionTest);
        AppLog.Info("ServiceController.TriggerTestAlert");
        ContextCompat.StartForegroundService(context, intent);
    }

    public static void StopAlert()
    {
        var context = AndroidApp.Context;
        var intent = new Intent(context, JavaClass.FromType(typeof(LiveAlertForegroundService)));
        intent.SetAction(ActionStopAlert);
        AppLog.Info("ServiceController.StopAlert");
        ContextCompat.StartForegroundService(context, intent);
    }

    public static void RefreshNotification()
    {
        var context = AndroidApp.Context;
        var intent = new Intent(context, JavaClass.FromType(typeof(LiveAlertForegroundService)));
        intent.SetAction(ActionRefreshNotification);
        AppLog.Info("ServiceController.RefreshNotification");
        ContextCompat.StartForegroundService(context, intent);
    }

    internal static void RaiseWarning(string message)
    {
        _warningActive = !string.IsNullOrWhiteSpace(message);
        _warningMessage = message;
        AppLog.Warn($"WarningChanged active={_warningActive} message={message}");
        WarningChanged?.Invoke(_warningActive, _warningMessage);
    }

    internal static void RaiseDebugMode(bool enabled)
    {
        _debugMode = enabled;
        AppLog.Info($"DebugModeChanged enabled={enabled}");
        DebugModeChanged?.Invoke(enabled);
    }
}
