using AudioPlayer.Core.Audio;
using AudioPlayer.Core.Model;

namespace AudioPlayer.Core.Tests;

public class WelcomeSupportTests
{
    private static string NewDir()
    {
        var d = Path.Combine(Path.GetTempPath(), $"ap-w-{Guid.NewGuid():N}");
        Directory.CreateDirectory(d);
        return d;
    }

    private static string Touch(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, []);
        return path;
    }

    [Fact]
    public void RecentIsMostRecentFirstWithoutDuplicates()
    {
        var dir = NewDir();
        string a = Touch(Path.Combine(dir, "a.approj")), b = Touch(Path.Combine(dir, "b.approj"));
        var recent = new RecentProjects(Path.Combine(dir, "recent.json"));

        recent.Add(a);
        recent.Add(b);
        recent.Add(a.ToUpperInvariant()); // same file, different case

        Assert.Equal([a.ToUpperInvariant(), b], recent.Load());
    }

    [Fact]
    public void RecentSkipsDeletedFilesAndCapsLength()
    {
        var dir = NewDir();
        var recent = new RecentProjects(Path.Combine(dir, "recent.json"), max: 3);
        var files = Enumerable.Range(0, 5).Select(i => Touch(Path.Combine(dir, $"{i}.approj"))).ToList();
        files.ForEach(recent.Add);
        File.Delete(files[4]);

        Assert.Equal([files[3], files[2]], recent.Load());
    }

    [Fact]
    public void RecentWithMissingOrCorruptFileIsEmpty()
    {
        var dir = NewDir();
        Assert.Empty(new RecentProjects(Path.Combine(dir, "none.json")).Load());
        File.WriteAllText(Path.Combine(dir, "bad.json"), "{not json");
        Assert.Empty(new RecentProjects(Path.Combine(dir, "bad.json")).Load());
    }

    [Fact]
    public void FolderScanFindsAudioRecursivelySorted()
    {
        var dir = NewDir();
        Touch(Path.Combine(dir, "b.MP3"));
        Touch(Path.Combine(dir, "a.wav"));
        Touch(Path.Combine(dir, "sub", "c.flac"));
        Touch(Path.Combine(dir, "notes.txt"));

        var found = AudioFiles.InFolder(dir).Select(p => Path.GetRelativePath(dir, p));

        Assert.Equal(["a.wav", "b.MP3", Path.Combine("sub", "c.flac")], found);
    }
}
