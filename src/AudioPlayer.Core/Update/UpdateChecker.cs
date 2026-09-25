using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

namespace AudioPlayer.Core.Update;

/// <summary>A published release that can be installed.</summary>
public sealed record ReleaseInfo(Version Version, string Tag, string PageUrl, string InstallerName, string InstallerUrl, string? ChecksumsUrl);

/// <summary>
/// Finds the latest GitHub release and downloads its installer, verified against the release's SHA256SUMS.
/// Network failures are the caller's to swallow: the app must work fully offline.
/// </summary>
public static class UpdateChecker
{
    public const string LatestReleaseApi = "https://api.github.com/repos/Imagaa/EVSEQ/releases/latest";

    /// <summary>"v0.2.1" / "0.2.1" → 0.2.1; null for pre-release or malformed tags (never offered as updates).</summary>
    public static Version? ParseTag(string tag)
    {
        var s = tag.Trim().TrimStart('v', 'V');
        return s.Contains('-') || !Version.TryParse(s, out var v) ? null : Normalize(v);
    }

    /// <summary>Informational version like "0.2.1+603e7a1" or "0.2.1-local" → 0.2.1 (build metadata/suffix dropped).</summary>
    public static Version? ParseAppVersion(string? informational)
    {
        if (string.IsNullOrWhiteSpace(informational)) return null;
        var s = informational.Split('+', '-')[0];
        return Version.TryParse(s, out var v) ? Normalize(v) : null;
    }

    public static bool IsNewer(Version latest, Version current) => Normalize(latest) > Normalize(current);

    /// <summary>Reads GitHub's "latest release" JSON. Null when it is a pre-release or has no installer.</summary>
    public static ReleaseInfo? ParseRelease(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.TryGetProperty("prerelease", out var pre) && pre.GetBoolean()) return null;
        if (root.TryGetProperty("draft", out var draft) && draft.GetBoolean()) return null;

        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (ParseTag(tag) is not { } version) return null;

        string? installerName = null, installerUrl = null, sumsUrl = null;
        foreach (var asset in root.GetProperty("assets").EnumerateArray())
        {
            var name = asset.GetProperty("name").GetString() ?? "";
            var url = asset.GetProperty("browser_download_url").GetString() ?? "";
            if (name.StartsWith("EVSEQ-Setup-", StringComparison.OrdinalIgnoreCase) && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                (installerName, installerUrl) = (name, url);
            else if (name.Equals("SHA256SUMS.txt", StringComparison.OrdinalIgnoreCase))
                sumsUrl = url;
        }
        if (installerName is null || installerUrl is null) return null;

        var page = root.TryGetProperty("html_url", out var html) ? html.GetString() ?? "" : "";
        return new ReleaseInfo(version, tag, page, installerName, installerUrl, sumsUrl);
    }

    /// <summary>Finds "HASH  file-name" in a SHA256SUMS text; null when the file is not listed.</summary>
    public static string? FindChecksum(string sums, string fileName) =>
        sums.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length >= 2 && parts[^1].TrimStart('*').Equals(fileName, StringComparison.OrdinalIgnoreCase))
            .Select(parts => parts[0].ToUpperInvariant())
            .FirstOrDefault();

    public static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    public static HttpClient CreateClient(Version current)
    {
        var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("EVSEQ", current.ToString()));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return http;
    }

    public static async Task<ReleaseInfo?> GetLatestAsync(HttpClient http, CancellationToken ct)
    {
        using var response = await http.GetAsync(LatestReleaseApi, ct);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null; // no releases yet
        response.EnsureSuccessStatusCode();
        return ParseRelease(await response.Content.ReadAsStringAsync(ct));
    }

    /// <summary>
    /// Downloads the installer into <paramref name="folder"/> and checks it against SHA256SUMS.
    /// A file that is already there and verifies is reused. Throws when the checksum does not match.
    /// </summary>
    public static async Task<string> DownloadInstallerAsync(HttpClient http, ReleaseInfo release, string folder, CancellationToken ct)
    {
        if (release.ChecksumsUrl is null) throw new InvalidDataException("Release tanpa SHA256SUMS; update tidak dipasang.");
        var sums = await http.GetStringAsync(release.ChecksumsUrl, ct);
        var expected = FindChecksum(sums, release.InstallerName)
                       ?? throw new InvalidDataException($"{release.InstallerName} tidak tercantum di SHA256SUMS.");

        Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, release.InstallerName);
        if (File.Exists(target) && Sha256(target) == expected) return target;

        var partial = target + ".part";
        await using (var source = await http.GetStreamAsync(release.InstallerUrl, ct))
        await using (var file = File.Create(partial))
            await source.CopyToAsync(file, ct);

        if (Sha256(partial) != expected)
        {
            File.Delete(partial);
            throw new InvalidDataException("Checksum installer tidak cocok; unduhan dibuang.");
        }
        File.Move(partial, target, overwrite: true);
        return target;
    }

    private static Version Normalize(Version v) => new(v.Major, v.Minor, Math.Max(0, v.Build));
}
