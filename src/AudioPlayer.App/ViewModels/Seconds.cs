using System.Globalization;

namespace AudioPlayer.App.ViewModels;

/// <summary>Fade durations are stored in ms but shown/edited in seconds ("0.4", "1,5" both accepted).</summary>
public static class Seconds
{
    public const double Max = 10;

    public static string Format(int ms) => (ms / 1000.0).ToString("0.###", CultureInfo.InvariantCulture);

    public static bool TryParseMs(string? text, out int ms)
    {
        ms = 0;
        if (!double.TryParse((text ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var s)
            || s is < 0 or > Max)
            return false;
        ms = (int)Math.Round(s * 1000);
        return true;
    }
}
