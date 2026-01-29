using Android.Content;
using Android.Graphics;
using Android.Views;
using AView = Android.Views.View;
using AColor = Android.Graphics.Color;
using APaint = Android.Graphics.Paint;
using APaintFlags = Android.Graphics.PaintFlags;

namespace LiveAlert;

public sealed class StripedView : AView
{
    private readonly APaint _stripePaint = new(APaintFlags.AntiAlias);
    private readonly APaint _backgroundPaint = new(APaintFlags.AntiAlias);
    private readonly int _stripeWidth;

    public StripedView(Context context, AColor background, AColor stripe, int stripeWidth) : base(context)
    {
        _backgroundPaint.Color = background;
        _stripePaint.Color = stripe;
        _stripeWidth = Math.Max(2, stripeWidth);
    }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);

        canvas.DrawRect(0, 0, Width, Height, _backgroundPaint);

        var diag = (float)Math.Sqrt(Width * Width + Height * Height);
        canvas.Save();
        canvas.Rotate(-45f);

        var step = _stripeWidth * 2;
        for (float x = -diag; x < diag * 2; x += step)
        {
            canvas.DrawRect(x, 0, x + _stripeWidth, diag * 2, _stripePaint);
        }

        canvas.Restore();
    }
}
