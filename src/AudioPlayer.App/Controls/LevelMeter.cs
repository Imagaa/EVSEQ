using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace AudioPlayer.App.Controls;

/// <summary>
/// Stereo peak meter (-60..0 dBFS): green / yellow above -18 dB / red above -6 dB, a peak-hold line per
/// channel, and a CLIP lamp on top that stays lit until clicked. The scale leaves 10 px at top and bottom
/// so it lines up with the fader tick marks next to it.
/// </summary>
public sealed class LevelMeter : FrameworkElement
{
    private static FrameworkPropertyMetadata Render(object d) => new(d, FrameworkPropertyMetadataOptions.AffectsRender);

    public static readonly DependencyProperty LeftDbProperty = DependencyProperty.Register(nameof(LeftDb), typeof(double), typeof(LevelMeter), Render(-60.0));
    public static readonly DependencyProperty RightDbProperty = DependencyProperty.Register(nameof(RightDb), typeof(double), typeof(LevelMeter), Render(-60.0));
    public static readonly DependencyProperty LeftHoldDbProperty = DependencyProperty.Register(nameof(LeftHoldDb), typeof(double), typeof(LevelMeter), Render(-60.0));
    public static readonly DependencyProperty RightHoldDbProperty = DependencyProperty.Register(nameof(RightHoldDb), typeof(double), typeof(LevelMeter), Render(-60.0));
    public static readonly DependencyProperty ClippedProperty = DependencyProperty.Register(nameof(Clipped), typeof(bool), typeof(LevelMeter), Render(false));
    public static readonly DependencyProperty ResetClipCommandProperty = DependencyProperty.Register(nameof(ResetClipCommand), typeof(ICommand), typeof(LevelMeter));

    public double LeftDb { get => (double)GetValue(LeftDbProperty); set => SetValue(LeftDbProperty, value); }
    public double RightDb { get => (double)GetValue(RightDbProperty); set => SetValue(RightDbProperty, value); }
    public double LeftHoldDb { get => (double)GetValue(LeftHoldDbProperty); set => SetValue(LeftHoldDbProperty, value); }
    public double RightHoldDb { get => (double)GetValue(RightHoldDbProperty); set => SetValue(RightHoldDbProperty, value); }
    public bool Clipped { get => (bool)GetValue(ClippedProperty); set => SetValue(ClippedProperty, value); }
    public ICommand? ResetClipCommand { get => (ICommand?)GetValue(ResetClipCommandProperty); set => SetValue(ResetClipCommandProperty, value); }

    private const double MinDb = -60, Reserved = 10, BarWidth = 6, Gap = 2;

    private static readonly Brush Groove = Frozen(0x12, 0x12, 0x16);
    private static readonly Brush Green = Frozen(0x3F, 0xB9, 0x50);
    private static readonly Brush Yellow = Frozen(0xD2, 0x99, 0x22);
    private static readonly Brush Red = Frozen(0xF8, 0x51, 0x49);
    private static readonly Brush LampOff = Frozen(0x3A, 0x1C, 0x1C);
    private static readonly Pen HoldPen = FrozenPen(Brushes.White, 2);

    public LevelMeter()
    {
        Width = BarWidth * 2 + Gap;
        Cursor = Cursors.Hand;
        ToolTip = "Peak meter (dBFS, setelah master fader). Lampu merah = clipping; klik untuk reset.";
    }

    protected override void OnRender(DrawingContext dc)
    {
        double top = Reserved, bottom = ActualHeight - Reserved, height = bottom - top;
        if (height <= 0) return;
        double Y(double db) => top + Math.Clamp(-db / -MinDb, 0, 1) * height; // 0 dB at top

        dc.DrawRectangle(Clipped ? Red : LampOff, null, new Rect(0, 0, Width, Reserved - 3)); // CLIP lamp

        DrawBar(dc, 0, LeftDb, LeftHoldDb, Y, top, bottom);
        DrawBar(dc, BarWidth + Gap, RightDb, RightHoldDb, Y, top, bottom);
    }

    private static void DrawBar(DrawingContext dc, double x, double levelDb, double holdDb, Func<double, double> y, double top, double bottom)
    {
        dc.DrawRectangle(Groove, null, new Rect(x, top, BarWidth, bottom - top));
        double level = y(levelDb);
        if (levelDb > MinDb)
        {
            // Colour zones, each clipped to the lit part of the bar.
            dc.PushClip(new RectangleGeometry(new Rect(x, level, BarWidth, Math.Max(0, bottom - level))));
            dc.DrawRectangle(Green, null, new Rect(x, y(-18), BarWidth, bottom - y(-18)));
            dc.DrawRectangle(Yellow, null, new Rect(x, y(-6), BarWidth, y(-18) - y(-6)));
            dc.DrawRectangle(Red, null, new Rect(x, top, BarWidth, y(-6) - top));
            dc.Pop();
        }
        if (holdDb > MinDb)
        {
            double h = y(holdDb);
            dc.DrawLine(HoldPen, new Point(x, h), new Point(x + BarWidth, h));
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        if (ResetClipCommand?.CanExecute(null) == true) ResetClipCommand.Execute(null);
        e.Handled = true;
    }

    private static SolidColorBrush Frozen(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    private static Pen FrozenPen(Brush b, double t)
    {
        var p = new Pen(b, t);
        p.Freeze();
        return p;
    }
}
