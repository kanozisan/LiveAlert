using System;
using Android.App;
using Android.Content;
using Android.Content.PM;
using AColor = Android.Graphics.Color;
using Android.OS;
using Android.Views;
using Android.Widget;
using LiveAlert.Core;

namespace LiveAlert;

[Activity(
    Name = "com.yasagurebug.livealert.AlertFullscreenActivity",
    Theme = "@style/Maui.MainTheme",
    Exported = false,
    LaunchMode = LaunchMode.SingleTop,
    ShowWhenLocked = true,
    TurnScreenOn = true,
    ExcludeFromRecents = true)]
public sealed class AlertFullscreenActivity : Activity
{
    public const string ExtraMessage = "message";
    public const string ExtraBackground = "background";
    public const string ExtraText = "text";
    public const string ActionDismiss = "com.yasagurebug.livealert.ALERT_DISMISS";

    private AlertBandLayout? _band;
    private BroadcastReceiver? _dismissReceiver;
    private BroadcastReceiver? _userPresentReceiver;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        ApplyLockScreenFlags();
        BuildLayout(Intent);
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);
        BuildLayout(intent);
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        AlertBandFactory.StopLayers(_band);
        _band = null;
    }

    protected override void OnStart()
    {
        base.OnStart();
        RegisterReceivers();
    }

    protected override void OnStop()
    {
        base.OnStop();
        UnregisterReceivers();
    }

    private void ApplyLockScreenFlags()
    {
        Window?.AddFlags(WindowManagerFlags.Fullscreen | WindowManagerFlags.KeepScreenOn);
        Window?.AddFlags(WindowManagerFlags.ShowWhenLocked | WindowManagerFlags.TurnScreenOn);
    }

    private void BuildLayout(Intent? intent)
    {
        AlertBandFactory.StopLayers(_band);
        _band = null;

        var displayMetrics = Resources?.DisplayMetrics;
        var screenHeight = displayMetrics?.HeightPixels ?? 0;
        var bandHeight = Math.Max(1, screenHeight / 2);
        var message = intent?.GetStringExtra(ExtraMessage) ?? string.Empty;
        var colors = new AlertColors
        {
            Background = intent?.GetStringExtra(ExtraBackground) ?? "#FF0000",
            Text = intent?.GetStringExtra(ExtraText) ?? "#000000"
        };

        var options = new AlertOptions
        {
            BandHeightPx = bandHeight
        };

        _band = AlertBandFactory.Build(this, message, options, colors, bandHeight);
        var root = _band.Root;
        root.Click += (_, _) =>
        {
            ServiceController.StopAlert();
            Finish();
        };

        var rootParams = new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, bandHeight)
        {
            Gravity = GravityFlags.Center
        };

        var container = new FrameLayout(this)
        {
            Clickable = true,
            Focusable = true
        };
        container.SetBackgroundColor(AColor.Black);
        container.AddView(root, rootParams);
        SetContentView(container);
    }

    private void RegisterReceivers()
    {
        if (_dismissReceiver == null)
        {
            _dismissReceiver = new ActionReceiver(() => Finish());
            RegisterReceiverCompat(_dismissReceiver, new IntentFilter(ActionDismiss));
        }

        if (_userPresentReceiver == null)
        {
            _userPresentReceiver = new ActionReceiver(() => Finish());
            RegisterReceiverCompat(_userPresentReceiver, new IntentFilter(Intent.ActionUserPresent));
        }
    }

    private void UnregisterReceivers()
    {
        if (_dismissReceiver != null)
        {
            UnregisterReceiver(_dismissReceiver);
            _dismissReceiver = null;
        }

        if (_userPresentReceiver != null)
        {
            UnregisterReceiver(_userPresentReceiver);
            _userPresentReceiver = null;
        }
    }

    private void RegisterReceiverCompat(BroadcastReceiver receiver, IntentFilter filter)
    {
        if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
        {
#pragma warning disable CA1416
            RegisterReceiver(receiver, filter, ReceiverFlags.NotExported);
#pragma warning restore CA1416
            return;
        }

        RegisterReceiver(receiver, filter);
    }

    private sealed class ActionReceiver : BroadcastReceiver
    {
        private readonly Action _onReceive;

        public ActionReceiver(Action onReceive)
        {
            _onReceive = onReceive;
        }

        public override void OnReceive(Context? context, Intent? intent)
        {
            _onReceive();
        }
    }
}
