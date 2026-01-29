using System;
using System.Collections.Generic;
using Android.Content;
using Android.Graphics;
using Android.Text;
using Android.Views;
using Android.Widget;
using LiveAlert.Core;
using AColor = Android.Graphics.Color;

namespace LiveAlert;

public sealed class AlertBandLayout
{
    public AlertBandLayout(FrameLayout root, int totalHeight, List<ScrollingLayerView> layers)
    {
        Root = root;
        TotalHeight = totalHeight;
        Layers = layers;
    }

    public FrameLayout Root { get; }
    public int TotalHeight { get; }
    public List<ScrollingLayerView> Layers { get; }
}

public static class AlertBandFactory
{
    private const double EmergencyCycleSeconds = 1.0;
    private const double StripeCycleSeconds = 0.2;
    private const double CenterCycleSeconds = 4.0;

    public static AlertBandLayout Build(Context context, string message, AlertOptions options, AlertColors colors, int? totalHeightOverride = null)
    {
        var layers = new List<ScrollingLayerView>();
        var frame = new FrameLayout(context)
        {
            Clickable = true,
            Focusable = false
        };

        var totalHeight = totalHeightOverride ?? Math.Max(options.BandHeightPx, AlertOverlay.MinBandHeightPx);
        var emergencyHeight = Math.Max(32, totalHeight / 6);
        var stripeHeight = Math.Max(16, emergencyHeight / 2);
        var centerHeight = totalHeight - (emergencyHeight * 2 + stripeHeight * 2);
        if (centerHeight < 80)
        {
            emergencyHeight = Math.Max(24, (totalHeight - 80) / 3);
            stripeHeight = Math.Max(12, emergencyHeight / 2);
            centerHeight = totalHeight - (emergencyHeight * 2 + stripeHeight * 2);
        }

        var layout = new LinearLayout(context)
        {
            Orientation = Orientation.Vertical
        };

        var displayWidth = context.Resources?.DisplayMetrics?.WidthPixels ?? 0;

        var emergencyTop = BuildEmergencyLayer(context, layers, displayWidth, emergencyHeight);
        var stripeTop = BuildStripeLayer(context, layers, displayWidth, stripeHeight);
        var center = BuildCenterLayer(context, layers, message, colors, displayWidth, centerHeight);
        var stripeBottom = BuildStripeLayer(context, layers, displayWidth, stripeHeight);
        var emergencyBottom = BuildEmergencyLayer(context, layers, displayWidth, emergencyHeight);

        layout.AddView(emergencyTop);
        layout.AddView(stripeTop);
        layout.AddView(center);
        layout.AddView(stripeBottom);
        layout.AddView(emergencyBottom);

        frame.AddView(layout, new FrameLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, totalHeight));

        return new AlertBandLayout(frame, totalHeight, layers);
    }

    public static void StopLayers(AlertBandLayout? layout)
    {
        if (layout == null) return;
        foreach (var layer in layout.Layers)
        {
            layer.Stop();
        }
        layout.Layers.Clear();
    }

    private static ScrollingLayerView BuildEmergencyLayer(Context context, List<ScrollingLayerView> layers, int displayWidth, int height)
    {
        var (bitmap, unitWidth) = CreateTextLayerBitmap(
            context,
            text: "EMERGENCY",
            displayWidth: displayWidth,
            height: height,
            background: AColor.Black,
            textColor: AColor.Red,
            typeface: Typeface.DefaultBold ?? Typeface.Default);

        return CreateLayerView(context, layers, bitmap, unitWidth, EmergencyCycleSeconds, height);
    }

    private static ScrollingLayerView BuildCenterLayer(Context context, List<ScrollingLayerView> layers, string message, AlertColors colors, int displayWidth, int height)
    {
        var typeface = LoadCenterTypeface(context);
        var background = ParseColor(colors.Background, AColor.Red);
        var textColor = ParseColor(colors.Text, AColor.Black);
        var (bitmap, unitWidth) = CreateTextLayerBitmap(
            context,
            text: message,
            displayWidth: displayWidth,
            height: height,
            background: background,
            textColor: textColor,
            typeface: typeface);

        return CreateLayerView(context, layers, bitmap, unitWidth, CenterCycleSeconds, height);
    }

    private static ScrollingLayerView BuildStripeLayer(Context context, List<ScrollingLayerView> layers, int displayWidth, int height)
    {
        var (bitmap, unitWidth) = CreateStripeTile(height);
        return CreateLayerView(context, layers, bitmap, unitWidth, StripeCycleSeconds, height, useShader: true, rotateShader: false, tileY: Shader.TileMode.Clamp!);
    }

    private static ScrollingLayerView CreateLayerView(Context context, List<ScrollingLayerView> layers, Bitmap bitmap, int unitWidth, double cycleSeconds, int height, bool useShader = false, bool rotateShader = false)
    {
        return CreateLayerView(context, layers, bitmap, unitWidth, cycleSeconds, height, useShader, rotateShader, Shader.TileMode.Clamp!);
    }

    private static ScrollingLayerView CreateLayerView(Context context, List<ScrollingLayerView> layers, Bitmap bitmap, int unitWidth, double cycleSeconds, int height, bool useShader, bool rotateShader, Shader.TileMode tileY)
    {
        var view = new ScrollingLayerView(context)
        {
            LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, height)
        };
        view.Configure(bitmap, unitWidth, cycleSeconds, useShader, rotateShader, rotateDegrees: -45f, tileY: tileY);
        layers.Add(view);
        return view;
    }

    private static (Bitmap Bitmap, int UnitWidthPx) CreateTextLayerBitmap(Context context, string text, int displayWidth, int height, AColor background, AColor textColor, Typeface? typeface)
    {
        var padding = Math.Max(6, height / 6);
        var textSize = Math.Max(12, height - padding * 2);

        typeface ??= Typeface.Default;
        var paint = new TextPaint(PaintFlags.AntiAlias)
        {
            Color = textColor,
            TextSize = textSize
        };
        paint.SetTypeface(typeface);

        var unitText = text.Trim() + "   ";
        var unitWidth = (int)Math.Ceiling(paint.MeasureText(unitText) + padding * 2);
        unitWidth = Math.Max(1, unitWidth);

        var internalWidth = CalculateInternalWidth(displayWidth, unitWidth);
        var bitmap = Bitmap.CreateBitmap(internalWidth, height, Bitmap.Config.Argb8888!);
        using var canvas = new Canvas(bitmap);
        canvas.DrawColor(background);

        var baseline = CalculateBaseline(paint, height);
        for (var x = 0; x < internalWidth; x += unitWidth)
        {
            canvas.DrawText(unitText, x + padding, baseline, paint);
        }

        return (bitmap, unitWidth);
    }

    private static AColor ParseColor(string value, AColor fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        try
        {
            return AColor.ParseColor(value);
        }
        catch
        {
            return fallback;
        }
    }

    private static (Bitmap Bitmap, int UnitWidthPx) CreateStripeTile(int height)
    {
        var stripeWidth = Math.Max(6, height / 3);
        var unitWidth = Math.Max(1, stripeWidth * 2);
        var bitmap = Bitmap.CreateBitmap(unitWidth, height, Bitmap.Config.Argb8888!);
        var pixels = new int[unitWidth * height];
        var background = AColor.Black.ToArgb();
        var stripe = AColor.Red.ToArgb();

        for (var y = 0; y < height; y++)
        {
            var rowOffset = y * unitWidth;
            for (var x = 0; x < unitWidth; x++)
            {
                var v = (x + y) % unitWidth;
                pixels[rowOffset + x] = v < stripeWidth ? stripe : background;
            }
        }

        bitmap.SetPixels(pixels, 0, unitWidth, 0, 0, unitWidth, height);
        return (bitmap, unitWidth);
    }

    private static int CalculateInternalWidth(int screenWidthPx, int unitWidthPx)
    {
        var safeWidth = Math.Max(screenWidthPx + unitWidthPx, screenWidthPx + 1);
        var repeats = (int)Math.Ceiling(safeWidth / (double)unitWidthPx);
        return Math.Max(unitWidthPx, repeats * unitWidthPx);
    }

    private static float CalculateBaseline(TextPaint paint, int height)
    {
        var metrics = paint.GetFontMetrics();
        if (metrics == null)
        {
            return height / 2f;
        }
        var textHeight = metrics.Descent - metrics.Ascent;
        return (height - textHeight) / 2f - metrics.Ascent;
    }

    private static Typeface LoadCenterTypeface(Context context)
    {
        var typeface = TryLoadTypeface(context, "TsukuhouShogoMin-OFL.ttf");
        if (typeface != null)
        {
            return typeface;
        }

        return Typeface.Create("serif", TypefaceStyle.Bold) ?? Typeface.Default!;
    }

    private static Typeface? TryLoadTypeface(Context context, string assetPath)
    {
        try
        {
            return Typeface.CreateFromAsset(context.Assets!, assetPath);
        }
        catch
        {
            return null;
        }
    }
}
