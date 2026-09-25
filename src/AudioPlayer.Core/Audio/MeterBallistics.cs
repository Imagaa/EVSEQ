namespace AudioPlayer.Core.Audio;

/// <summary>
/// Peak-meter behaviour for one channel: instant attack, steady fall, a peak-hold marker,
/// and a clip flag that stays on until reset.
/// </summary>
public sealed class MeterBallistics
{
    public const double FloorDb = -60;
    public const double FallDbPerSecond = 24;
    public const double HoldSeconds = 1.5;
    /// <summary>Samples at or above this (linear) count as clipping: -0.01 dBFS.</summary>
    public const float ClipLevel = 0.9989f;

    private double holdAge;

    public double LevelDb { get; private set; } = FloorDb;
    public double HoldDb { get; private set; } = FloorDb;
    public bool Clipped { get; private set; }

    /// <param name="peak">Linear peak measured since the previous update.</param>
    /// <param name="elapsedSeconds">Time since the previous update.</param>
    public void Update(float peak, double elapsedSeconds)
    {
        double db = peak <= 0 ? FloorDb : Math.Max(FloorDb, 20 * Math.Log10(peak));
        LevelDb = Math.Max(db, LevelDb - FallDbPerSecond * elapsedSeconds);

        if (db >= HoldDb)
        {
            HoldDb = db;
            holdAge = 0;
        }
        else
        {
            holdAge += elapsedSeconds;
            if (holdAge > HoldSeconds) HoldDb = Math.Max(LevelDb, HoldDb - FallDbPerSecond * elapsedSeconds);
        }

        if (peak >= ClipLevel) Clipped = true;
    }

    public void ResetClip() => Clipped = false;
}
