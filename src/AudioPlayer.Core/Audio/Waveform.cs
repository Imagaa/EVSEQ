using NAudio.Wave;

namespace AudioPlayer.Core.Audio;

public static class Waveform
{
    /// <summary>
    /// Peak level (0..1, max over all channels) for each of <paramref name="buckets"/> equal slices of the file.
    /// Decodes the whole file: run it off the UI thread.
    /// </summary>
    public static float[] ComputePeaks(string path, int buckets = 2000, CancellationToken ct = default)
    {
        using var reader = AudioFiles.OpenReader(path);
        var samples = (ISampleProvider)reader;
        long totalSamples = reader.Length / (reader.WaveFormat.BitsPerSample / 8);
        var peaks = new float[buckets];
        if (totalSamples == 0) return peaks;

        double samplesPerBucket = (double)totalSamples / buckets;
        var buffer = new float[reader.WaveFormat.SampleRate * reader.WaveFormat.Channels]; // ~1 s per read
        long index = 0;
        int n;
        while ((n = samples.Read(buffer)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            for (int i = 0; i < n; i++, index++)
            {
                int b = Math.Min(buckets - 1, (int)(index / samplesPerBucket));
                float v = Math.Abs(buffer[i]);
                if (v > peaks[b]) peaks[b] = v;
            }
        }
        for (int b = 0; b < buckets; b++) peaks[b] = Math.Min(1f, peaks[b]);
        return peaks;
    }
}
