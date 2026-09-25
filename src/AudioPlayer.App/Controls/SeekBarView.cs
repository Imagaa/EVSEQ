using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using AudioPlayer.Core.Audio;

namespace AudioPlayer.App.Controls;

/// <summary>
/// Seek bar drawn as a volume envelope: fade-in ramp, flat body, fade-out ramp (when an end point is set),
/// with the region outside start/end dimmed, the played part brighter, and a playhead.
/// Click or drag to seek; hovering shows the time under the cursor.
/// </summary>
public sealed class SeekBarView : FrameworkElement
{
    private static FrameworkPropertyMetadata Render(double d) => new(d, FrameworkPropertyMetadataOptions.AffectsRender);

    public static readonly DependencyProperty DurationProperty = DependencyProperty.Register(nameof(Duration), typeof(double), typeof(SeekBarView), Render(1));
    public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(nameof(Position), typeof(double), typeof(SeekBarView),
        new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender | FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public static readonly DependencyProperty RangeStartProperty = DependencyProperty.Register(nameof(RangeStart), typeof(double), typeof(SeekBarView), Render(0));
    public static readonly DependencyProperty RangeEndProperty = DependencyProperty.Register(nameof(RangeEnd), typeof(double), typeof(SeekBarView), Render(0));
    public static readonly DependencyProperty FadeInProperty = DependencyProperty.Register(nameof(FadeIn), typeof(double), typeof(SeekBarView), Render(0));
    public static readonly DependencyProperty FadeOutProperty = DependencyProperty.Register(nameof(FadeOut), typeof(double), typeof(SeekBarView), Render(0));
    public static readonly DependencyProperty ShowFadeOutProperty = DependencyProperty.Register(nameof(ShowFadeOut), typeof(bool), typeof(SeekBarView),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty CurveProperty = DependencyProperty.Register(nameof(Curve), typeof(FadeCurve), typeof(SeekBarView),
        new FrameworkPropertyMetadata(FadeCurve.EqualPower, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(nameof(Accent), typeof(Brush), typeof(SeekBarView),
        new FrameworkPropertyMetadata(Brushes.SteelBlue, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty IsScrubbingProperty = DependencyProperty.Register(nameof(IsScrubbing), typeof(bool), typeof(SeekBarView),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public double Duration { get => (double)GetValue(DurationProperty); set => SetValue(DurationProperty, value); }
    public double Position { get => (double)GetValue(PositionProperty); set => SetValue(PositionProperty, value); }
    public double RangeStart { get => (double)GetValue(RangeStartProperty); set => SetValue(RangeStartProperty, value); }
    public double RangeEnd { get => (double)GetValue(RangeEndProperty); set => SetValue(RangeEndProperty, value); }
    public double FadeIn { get => (double)GetValue(FadeInProperty); set => SetValue(FadeInProperty, value); }
    public double FadeOut { get => (double)GetValue(FadeOutProperty); set => SetValue(FadeOutProperty, value); }
    public bool ShowFadeOut { get => (bool)GetValue(ShowFadeOutProperty); set => SetValue(ShowFadeOutProperty, value); }
    public FadeCurve Curve { get => (FadeCurve)GetValue(CurveProperty); set => SetValue(CurveProperty, value); }
    public Brush Accent { get => (Brush)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }

    /// <summary>True while the user drags; the view model seeks once when it turns false.</summary>
    public bool IsScrubbing { get => (bool)GetValue(IsScrubbingProperty); set => SetValue(IsScrubbingProperty, value); }

    private double? hoverX;

    private static readonly Brush Groove = Frozen(Color.FromRgb(0x17, 0x17, 0x1B));
    private static readonly Brush Outside = Frozen(Color.FromArgb(0xB0, 0x0E, 0x0E, 0x10));
    private static readonly Brush MarkerBrush = Frozen(Color.FromRgb(0xEC, 0xEC, 0xEF));
    private static readonly Brush LabelBg = Frozen(Color.FromArgb(0xE6, 0x2D, 0x2D, 0x30));
    private static readonly Pen PlayheadPen = FrozenPen(Brushes.White, 2);
    private static readonly Pen HoverPen = FrozenPen(new SolidColorBrush(Color.FromArgb(0x90, 0xFF, 0xFF, 0xFF)), 1);
    private static readonly Pen MarkerPen = FrozenPen(MarkerBrush, 1.5);

    public SeekBarView()
    {
        Cursor = Cursors.Hand;
        MinHeight = 44;
        SnapsToDevicePixels = true;
        IsEnabledChanged += (_, _) => InvalidateVisual();
    }

    protected override void OnRender(DrawingContext dc)
    {
        double w = ActualWidth, h = ActualHeight;
        const double top = 14; // room for the hover label
        double bottom = h - 2;
        dc.DrawRoundedRectangle(Groove, null, new Rect(0, top, w, bottom - top), 4, 4);
        if (!IsEnabled || Duration <= 0 || w <= 0) return;

        double X(double seconds) => Math.Clamp(seconds / Duration, 0, 1) * w;
        double start = Math.Clamp(RangeStart, 0, Duration);
        double end = Math.Clamp(RangeEnd <= start ? Duration : RangeEnd, start, Duration);

        // Envelope inside the range: rise over fade-in, flat, fall over fade-out.
        var accent = Accent;
        var envelope = Envelope(X, start, end, top + 3, bottom - 1);
        dc.PushOpacity(0.28);
        dc.DrawGeometry(accent, null, envelope);
        dc.Pop();
        dc.PushClip(new RectangleGeometry(new Rect(X(start), 0, Math.Max(0, X(Position) - X(start)), h)));
        dc.PushOpacity(0.85);
        dc.DrawGeometry(accent, null, envelope);
        dc.Pop();
        dc.Pop();
        dc.DrawGeometry(null, new Pen(accent, 1.2), envelope);

        // Outside the start/end range
        if (start > 0) dc.DrawRectangle(Outside, null, new Rect(0, top, X(start), bottom - top));
        if (end < Duration) dc.DrawRectangle(Outside, null, new Rect(X(end), top, w - X(end), bottom - top));

        // Start/end flags
        DrawMarker(dc, X(start), top, bottom, pointsRight: true);
        DrawMarker(dc, X(end), top, bottom, pointsRight: false);

        // Playhead
        double px = X(Position);
        dc.DrawLine(PlayheadPen, new Point(px, top - 2), new Point(px, bottom));
        dc.DrawEllipse(Brushes.White, null, new Point(px, top), 5, 5);

        // Hover time
        if (hoverX is { } hx)
        {
            dc.DrawLine(HoverPen, new Point(hx, top), new Point(hx, bottom));
            var text = new FormattedText(Fmt(hx / w * Duration), CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface("Segoe UI"), 11, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            double lx = Math.Clamp(hx - text.Width / 2, 0, Math.Max(0, w - text.Width - 6));
            dc.DrawRoundedRectangle(LabelBg, null, new Rect(lx, 0, text.Width + 6, 13), 2, 2);
            dc.DrawText(text, new Point(lx + 3, -1));
        }
    }

    private StreamGeometry Envelope(Func<double, double> x, double start, double end, double top, double bottom)
    {
        double len = end - start;
        double fin = Math.Min(Math.Max(0, FadeIn), len);
        double fout = ShowFadeOut ? Math.Min(Math.Max(0, FadeOut), len - fin) : 0;
        double Y(float gain) => bottom - gain * (bottom - top);

        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(x(start), bottom), isFilled: true, isClosed: true);
            const int steps = 24;
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                c.LineTo(new Point(x(start + fin * t), Y(FadeCurves.Shape(Curve, t, rising: true))), true, false);
            }
            c.LineTo(new Point(x(end - fout), top), true, false);
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                c.LineTo(new Point(x(end - fout + fout * t), Y(1 - FadeCurves.Shape(Curve, t, rising: false))), true, false);
            }
            c.LineTo(new Point(x(end), bottom), true, false);
        }
        g.Freeze();
        return g;
    }

    private static void DrawMarker(DrawingContext dc, double x, double top, double bottom, bool pointsRight)
    {
        dc.DrawLine(MarkerPen, new Point(x, top), new Point(x, bottom));
        double d = pointsRight ? 7 : -7;
        var flag = new StreamGeometry();
        using (var c = flag.Open())
        {
            c.BeginFigure(new Point(x, top), true, true);
            c.LineTo(new Point(x + d, top), true, false);
            c.LineTo(new Point(x, top + 7), true, false);
        }
        flag.Freeze();
        dc.DrawGeometry(MarkerBrush, null, flag);
    }

    // ---- mouse: click/drag to seek ----

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (!IsEnabled || Duration <= 0) return;
        CaptureMouse();
        IsScrubbing = true;
        SeekTo(e.GetPosition(this).X);
        e.Handled = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        hoverX = Math.Clamp(e.GetPosition(this).X, 0, ActualWidth);
        if (IsMouseCaptured) SeekTo(hoverX.Value);
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        hoverX = null;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (!IsMouseCaptured) return;
        SeekTo(e.GetPosition(this).X);
        ReleaseMouseCapture(); // OnLostMouseCapture ends the scrub
        e.Handled = true;
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        if (IsScrubbing) IsScrubbing = false;
    }

    private void SeekTo(double x)
    {
        if (ActualWidth <= 0) return;
        SetCurrentValue(PositionProperty, Math.Clamp(x / ActualWidth, 0, 1) * Duration);
    }

    private static string Fmt(double seconds) => TimeSpan.FromSeconds(Math.Max(0, seconds)).ToString(@"mm\:ss\.f");

    private static SolidColorBrush Frozen(Color c)
    {
        var b = new SolidColorBrush(c);
        b.Freeze();
        return b;
    }

    private static Pen FrozenPen(Brush b, double thickness)
    {
        var p = new Pen(b, thickness);
        p.Freeze();
        return p;
    }
}
