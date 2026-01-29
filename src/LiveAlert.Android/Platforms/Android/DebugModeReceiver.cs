using System;
using System.IO;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using LiveAlert.Core;
using AndroidApp = Android.App.Application;

namespace LiveAlert;

[BroadcastReceiver(Enabled = true, Exported = true, Name = "com.yasagurebug.livealert.DebugModeReceiver")]
[IntentFilter(new[] { ActionDebugSet })]
public sealed class DebugModeReceiver : BroadcastReceiver
{
    public const string ActionDebugSet = "com.yasagurebug.livealert.DEBUG_SET";
    public const string ExtraEnabled = "enabled";

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action != ActionDebugSet) return;

        var enabled = intent.GetBooleanExtra(ExtraEnabled, false);
#if DEBUG
        Android.Util.Log.WriteLine(Android.Util.LogPriority.Info, "LiveAlert", $"DebugModeReceiver.OnReceive start enabled={enabled}");
#endif
        var pendingResult = GoAsync();
        _ = Task.Run(async () =>
        {
            try
            {
#if DEBUG
                Android.Util.Log.WriteLine(Android.Util.LogPriority.Info, "LiveAlert", "DebugModeReceiver resolving baseDir");
#endif
                var baseDir = AndroidApp.Context.FilesDir?.AbsolutePath ?? string.Empty;
#if DEBUG
                Android.Util.Log.WriteLine(Android.Util.LogPriority.Info, "LiveAlert", $"DebugModeReceiver baseDir='{baseDir}'");
#endif
                var configPath = Path.Combine(baseDir, "config.json");
#if DEBUG
                Android.Util.Log.WriteLine(Android.Util.LogPriority.Info, "LiveAlert", $"DebugModeReceiver configPath='{configPath}'");
#endif
                var manager = new ConfigManager(configPath);
#if DEBUG
                Android.Util.Log.WriteLine(Android.Util.LogPriority.Info, "LiveAlert", "DebugModeReceiver LoadAsync start");
#endif
                await manager.LoadAsync().ConfigureAwait(false);
#if DEBUG
                Android.Util.Log.WriteLine(Android.Util.LogPriority.Info, "LiveAlert", "DebugModeReceiver LoadAsync done");
#endif
                var config = manager.Current;
                config.Options.DebugMode = enabled;
#if DEBUG
                Android.Util.Log.WriteLine(Android.Util.LogPriority.Info, "LiveAlert", "DebugModeReceiver SaveAsync start");
#endif
                await manager.SaveAsync(config).ConfigureAwait(false);
#if DEBUG
                Android.Util.Log.WriteLine(Android.Util.LogPriority.Info, "LiveAlert", "DebugModeReceiver SaveAsync done");
#endif
                ServiceController.RaiseDebugMode(enabled);
                AppLog.Info($"DebugModeReceiver enabled={enabled}");
            }
            catch (Exception ex)
            {
#if DEBUG
                Android.Util.Log.WriteLine(Android.Util.LogPriority.Warn, "LiveAlert", $"DebugModeReceiver exception: {ex}");
#endif
                AppLog.Warn($"DebugModeReceiver failed: {ex.Message}");
            }
            finally
            {
                pendingResult?.Finish();
            }
        });
    }
}
