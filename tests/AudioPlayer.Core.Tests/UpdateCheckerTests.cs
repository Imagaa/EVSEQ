using AudioPlayer.Core.Update;

namespace AudioPlayer.Core.Tests;

public class UpdateCheckerTests
{
    private const string LatestJson = """
        {
          "tag_name": "v0.2.1", "draft": false, "prerelease": false,
          "html_url": "https://github.com/Imagaa/EVSEQ/releases/tag/v0.2.1",
          "assets": [
            { "name": "EVSEQ-Setup-0.2.1.exe", "browser_download_url": "https://example.test/EVSEQ-Setup-0.2.1.exe" },
            { "name": "SHA256SUMS.txt", "browser_download_url": "https://example.test/SHA256SUMS.txt" }
          ]
        }
        """;

    [Theory]
    [InlineData("v0.2.1", "0.2.1")]
    [InlineData("0.3.0", "0.3.0")]
    [InlineData("v1.2", "1.2.0")]
    public void ParsesReleaseTags(string tag, string expected) => Assert.Equal(Version.Parse(expected), UpdateChecker.ParseTag(tag));

    [Theory]
    [InlineData("v0.3.0-beta")]
    [InlineData("0.0.0-ci-test")]
    [InlineData("latest")]
    public void PreReleaseOrOddTagsAreNeverUpdates(string tag) => Assert.Null(UpdateChecker.ParseTag(tag));

    [Theory]
    [InlineData("0.2.1+603e7a1971288e7ba3bc8577541f5805a1d40280", "0.2.1")]
    [InlineData("0.0.0-local+603e7a1", "0.0.0")]
    [InlineData("0.2.0", "0.2.0")]
    public void ReadsTheAppsOwnVersion(string informational, string expected) =>
        Assert.Equal(Version.Parse(expected), UpdateChecker.ParseAppVersion(informational));

    [Theory]
    [InlineData("0.2.1", "0.2.0", true)]
    [InlineData("0.3.0", "0.2.9", true)]
    [InlineData("0.2.0", "0.2.0", false)]
    [InlineData("0.1.9", "0.2.0", false)]
    [InlineData("0.2.0", "0.2", false)] // 0.2 == 0.2.0
    public void ComparesVersions(string latest, string current, bool newer) =>
        Assert.Equal(newer, UpdateChecker.IsNewer(Version.Parse(latest), Version.Parse(current)));

    [Fact]
    public void ReadsInstallerAndChecksumsFromLatestRelease()
    {
        var r = UpdateChecker.ParseRelease(LatestJson)!;
        Assert.Equal(new Version(0, 2, 1), r.Version);
        Assert.Equal("EVSEQ-Setup-0.2.1.exe", r.InstallerName);
        Assert.Equal("https://example.test/EVSEQ-Setup-0.2.1.exe", r.InstallerUrl);
        Assert.Equal("https://example.test/SHA256SUMS.txt", r.ChecksumsUrl);
    }

    [Fact]
    public void IgnoresPreReleasesAndReleasesWithoutInstaller()
    {
        Assert.Null(UpdateChecker.ParseRelease(LatestJson.Replace("\"prerelease\": false", "\"prerelease\": true")));
        Assert.Null(UpdateChecker.ParseRelease(LatestJson.Replace("EVSEQ-Setup-0.2.1.exe\"", "notes.txt\"")));
    }

    [Fact]
    public void FindsChecksumForTheInstaller()
    {
        var sums = "ABC123  other.exe\r\nb16a6fcc965fe473cbc48611915c2c219ebc0ad5ecd99259930078b40b0f3b88  EVSEQ-Setup-0.2.1.exe\r\n";
        Assert.Equal("B16A6FCC965FE473CBC48611915C2C219EBC0AD5ECD99259930078B40B0F3B88", UpdateChecker.FindChecksum(sums, "EVSEQ-Setup-0.2.1.exe"));
        Assert.Null(UpdateChecker.FindChecksum(sums, "EVSEQ-Setup-9.9.9.exe"));
    }

    [Fact]
    public void Sha256MatchesKnownValue()
    {
        var path = Path.Combine(Path.GetTempPath(), $"ap-sha-{Guid.NewGuid():N}.txt");
        File.WriteAllText(path, "abc");
        Assert.Equal("BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD", UpdateChecker.Sha256(path));
    }
}
