using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace AudioPlayer.Core.Audio;

/// <summary>
/// Waveform peaks kept on disk so reopening a project does not decode every file again. The key covers the
/// file's full path, size and modification time, so an edited or replaced file is decoded afresh.
/// </summary>
// ponytail: entries are never pruned; ~8 KB each, add cleanup if libraries grow into the thousands
public sealed class WaveformCache(string folder)
{
    private const string Format = "v1";

    public float[] GetOrCompute(string path, int buckets = 2000, CancellationToken ct = default)
    {
        var file = CacheFile(path, buckets);
        if (file is not null && TryRead(file, buckets) is { } cached) return cached;

        var peaks = Waveform.ComputePeaks(path, buckets, ct);
        if (file is not null) TryWrite(file, peaks);
        return peaks;
    }

    private string? CacheFile(string path, int buckets)
    {
        var info = new FileInfo(path);
        if (!info.Exists) return null;
        var key = $"{Format}|{info.FullName.ToUpperInvariant()}|{info.Length}|{info.LastWriteTimeUtc.Ticks}|{buckets}";
        var name = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(key)));
        return Path.Combine(folder, name + ".peaks");
    }

    private static float[]? TryRead(string file, int buckets)
    {
        try
        {
            var bytes = File.ReadAllBytes(file);
            return bytes.Length == buckets * sizeof(float) ? MemoryMarshal.Cast<byte, float>(bytes).ToArray() : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null; // missing or unreadable: just decode again
        }
    }

    private void TryWrite(string file, float[] peaks)
    {
        try
        {
            Directory.CreateDirectory(folder);
            var tmp = file + ".tmp";
            File.WriteAllBytes(tmp, MemoryMarshal.AsBytes(peaks.AsSpan()).ToArray());
            File.Move(tmp, file, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // cache is an optimisation only
        }
    }
}
