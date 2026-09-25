namespace AudioPlayer.Core.Audio;

public static class Db
{
    /// <summary>At or below this level the gain is treated as silence.</summary>
    public const double Floor = -60;

    public static float ToGain(double db) => db <= Floor ? 0f : (float)Math.Pow(10, db / 20);

    public static double FromGain(float gain) => gain <= 0 ? double.NegativeInfinity : 20 * Math.Log10(gain);
}
