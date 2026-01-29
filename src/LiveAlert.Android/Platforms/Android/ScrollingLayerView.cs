using Android.Animation;
using Android.Content;
using Android.Graphics;
using Android.Views;
using Android.Views.Animations;
using AView = Android.Views.View;
using APaint = Android.Graphics.Paint;
using APaintFlags = Android.Graphics.PaintFlags;

namespace LiveAlert;

public sealed class ScrollingLayerView : AView
{
    private Bitmap? _bitmap;
    private int _unitWidth;
    private float _offset;
    private ValueAnimator? _animator;
    private readonly APaint _paint = new(APaintFlags.FilterBitmap | APaintFlags.AntiAlias);
    private bool _useShader;
    private BitmapShader? _shader;
    private readonly Matrix _shaderMatrix = new();
    private bool _rotateShader;
    private float _rotateDegrees;

    public ScrollingLayerView(Context context) : base(context)
    {
    }

    public void Configure(Bitmap bitmap, int unitWidthPx, double cycleSeconds)
    {
        Configure(bitmap, unitWidthPx, cycleSeconds, useShader: false, rotateShader: false, rotateDegrees: -45f, tileY: Shader.TileMode.Clamp!);
    }

    public void Configure(Bitmap bitmap, int unitWidthPx, double cycleSeconds, bool useShader, bool rotateShader, float rotateDegrees, Shader.TileMode tileY)
    {
        Stop();

        _bitmap = bitmap;
        _unitWidth = Math.Max(1, unitWidthPx);
        _offset = 0;
        _useShader = useShader;
        _rotateShader = rotateShader;
        _rotateDegrees = rotateDegrees;
        var resolvedTileY = tileY == null ? Shader.TileMode.Clamp! : tileY;
        if (_useShader)
        {
            _shader = new BitmapShader(bitmap, Shader.TileMode.Repeat!, resolvedTileY);
            _paint.SetShader(_shader);
        }
        else
        {
            _shader = null;
            _paint.SetShader(null);
        }

        var durationMs = (long)Math.Max(50.0, cycleSeconds * 1000.0);
        var animator = ValueAnimator.OfFloat(0f, _unitWidth);
        if (animator == null)
        {
            return;
        }
        _animator = animator;
        animator.SetDuration(durationMs);
        animator.SetInterpolator(new LinearInterpolator());
        animator.RepeatCount = ValueAnimator.Infinite;
        animator.RepeatMode = ValueAnimatorRepeatMode.Restart;
        animator.Update += (_, args) =>
        {
            var animated = args.Animation?.AnimatedValue;
            if (animated is Java.Lang.Float f)
            {
                _offset = f.FloatValue();
            }
            else if (animated is Java.Lang.Double d)
            {
                _offset = (float)d.DoubleValue();
            }
            else
            {
                return;
            }
            Invalidate();
        };
        animator.Start();
    }

    public void Stop()
    {
        if (_animator != null)
        {
            _animator.Cancel();
            _animator.Dispose();
            _animator = null;
        }

        if (_bitmap != null)
        {
            try
            {
                if (!_bitmap.IsRecycled)
                {
                    _bitmap.Recycle();
                }
            }
            catch
            {
            }
            _bitmap.Dispose();
            _bitmap = null;
        }
        _shader = null;
        _paint.SetShader(null);
    }

    protected override void OnDraw(Canvas canvas)
    {
        base.OnDraw(canvas);
        if (_bitmap == null) return;
        if (_useShader && _shader != null)
        {
            _shaderMatrix.Reset();
            _shaderMatrix.SetTranslate(-_offset, 0);
            if (_rotateShader)
            {
                _shaderMatrix.PostRotate(_rotateDegrees, Width / 2f, Height / 2f);
            }
            _shader.SetLocalMatrix(_shaderMatrix);
            canvas.DrawRect(0, 0, Width, Height, _paint);
            return;
        }
        canvas.DrawBitmap(_bitmap, -_offset, 0, _paint);
    }

    protected override void OnDetachedFromWindow()
    {
        base.OnDetachedFromWindow();
        Stop();
    }
}
