namespace AudioPlayer.Core.Audio;

public enum FadeCurve { Linear, EqualPower }

public static class FadeCurves
{
    /// <summary>Progress (0..1) from start gain to end gain at normalized time t.</summary>
    public static float Shape(FadeCurve curve, float t, bool rising) => curve switch
    {
        FadeCurve.EqualPower => rising ? MathF.Sin(t * MathF.PI / 2) : 1 - MathF.Cos(t * MathF.PI / 2),
        _ => t,
    };
}
