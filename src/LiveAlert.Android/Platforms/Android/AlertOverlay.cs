using Android.Content;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using LiveAlert.Core;

namespace LiveAlert;

public sealed class AlertOverlay
{
    private const string OverlayPermissionWarning = "画面の権限が付与されていません。端末の再起動が必要な場合があります";

    private readonly Context _context;
    private readonly IWindowManager _windowManager;
    private Android.Views.View? _rootView;
    private readonly List<ScrollingLayerView> _layerViews = new();
    private AlertBandLayout? _currentBand;

    public const int MinBandHeightPx = 256;
    public event Action? Tapped;

    public AlertOverlay(Context context)
    {
        _context = context;
        var service = _context.GetSystemService(Context.WindowService);
        if (service == null)
        {
            throw new InvalidOperationException("WindowManager service is unavailable.");
        }
        _windowManager = service.JavaCast<IWindowManager>();
    }

    public void Show(string message, AlertOptions options, AlertColors colors, bool showWhenLocked)
    {
        AppLog.Info($"Overlay.Show messageLen={message.Length} position={options.BandPosition} height={options.BandHeightPx}");
        Hide();

        var band = AlertBandFactory.Build(_context, message, options, colors, null);
        _currentBand = band;
        _layerViews.Clear();
        _layerViews.AddRange(band.Layers);
        var frame = band.Root;
        frame.Click += (_, _) => Tapped?.Invoke();
        var totalHeight = band.TotalHeight;

        var type = Build.VERSION.SdkInt >= BuildVersionCodes.O
            ? WindowManagerTypes.ApplicationOverlay
            : WindowManagerTypes.Phone;

        var flags = WindowManagerFlags.NotFocusable
            | WindowManagerFlags.LayoutInScreen
            | WindowManagerFlags.NotTouchModal;
        if (showWhenLocked)
        {
            flags |= WindowManagerFlags.ShowWhenLocked | WindowManagerFlags.KeepScreenOn;
        }

        var layoutParams = new WindowManagerLayoutParams(
            ViewGroup.LayoutParams.MatchParent,
            totalHeight,
            type,
            flags,
            Format.Translucent)
        {
            Gravity = options.BandPosition.Equals("bottom", StringComparison.OrdinalIgnoreCase)
                ? GravityFlags.Bottom
                : options.BandPosition.Equals("center", StringComparison.OrdinalIgnoreCase)
                    ? GravityFlags.CenterVertical
                    : GravityFlags.Top
        };

        _rootView = frame;
        try
        {
            _windowManager.AddView(frame, layoutParams);
            if (ServiceController.WarningMessage == OverlayPermissionWarning)
            {
                ServiceController.RaiseWarning(string.Empty);
            }
        }
        catch (Exception ex)
        {
            _rootView = null;
            AppLog.Error("Overlay.AddView failed", ex);
            if (ex is Android.Views.WindowManagerBadTokenException)
            {
                ServiceController.RaiseWarning(OverlayPermissionWarning);
            }
            return;
        }
    }

    public void Hide()
    {
        if (_rootView == null) return;
        try
        {
            AlertBandFactory.StopLayers(_currentBand);
            _layerViews.Clear();
            _windowManager.RemoveView(_rootView);
        }
        catch (Exception ex)
        {
            AppLog.Error("Overlay.RemoveView failed", ex);
        }
        finally
        {
            _rootView = null;
            _currentBand = null;
        }
    }
}
