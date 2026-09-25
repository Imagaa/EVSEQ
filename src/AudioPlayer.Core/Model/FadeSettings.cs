using AudioPlayer.Core.Audio;

namespace AudioPlayer.Core.Model;

public sealed class FadeSettings
{
    public int FadeInMs { get; set; } = 400;
    public int FadeOutMs { get; set; } = 400;
    public FadeCurve Curve { get; set; } = FadeCurve.EqualPower;
}
