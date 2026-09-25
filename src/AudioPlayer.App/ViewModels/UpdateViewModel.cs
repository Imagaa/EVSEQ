using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using AudioPlayer.Core.Update;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AudioPlayer.App.ViewModels;

/// <summary>
/// In-app updates: finds a newer GitHub release, downloads and verifies its installer in the background,
/// and installs it when EVSEQ closes (or right away on request). Never interrupts a running show and
/// stays silent when offline.
/// </summary>
public sealed partial class UpdateViewModel : ObservableObject
{
    private readonly MainViewModel main;
    private readonly string settingsFile, downloadFolder;
    private readonly AppSettings settings;
    private string? installerPath;
    private bool checking;

    public UpdateViewModel(MainViewModel main, string appDataDir)
    {
        this.main = main;
        settingsFile = Path.Combine(appDataDir, "settings.json");
        downloadFolder = Path.Combine(appDataDir, "updates");
        settings = AppSettings.Load(settingsFile);

        // EVSEQ_PRETEND_VERSION lets a developer test the update flow against real releases.
        var info = Environment.GetEnvironmentVariable("EVSEQ_PRETEND_VERSION")
                   ?? Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        CurrentVersion = UpdateChecker.ParseAppVersion(info) ?? new Version(0, 0, 0);
    }

    public Version CurrentVersion { get; }
    public string CurrentVersionText => $"v{CurrentVersion}";

    public bool AutoUpdate
    {
        get => settings.AutoUpdate;
        set
        {
            if (settings.AutoUpdate == value) return;
            settings.AutoUpdate = value;
            settings.Save(settingsFile);
            OnPropertyChanged();
        }
    }

    [ObservableProperty] public partial bool IsReady { get; set; }
    [ObservableProperty] public partial string ReadyVersion { get; set; } = "";
    [ObservableProperty] public partial bool BannerVisible { get; set; }
    [ObservableProperty] public partial string StatusText { get; set; } = "";

    /// <summary>Raised by "Pasang sekarang": the window closes (asking to save first) and installs.</summary>
    public event Action? InstallNowRequested;

    [RelayCommand] private void InstallNow() => InstallNowRequested?.Invoke();
    [RelayCommand] private void HideBanner() => BannerVisible = false;
    [RelayCommand] private Task CheckNow() => CheckAsync(userInitiated: true);

    /// <summary>Startup check (only when enabled) and the manual "Cek update" button.</summary>
    public async Task CheckAsync(bool userInitiated)
    {
        if (checking || IsReady) return;
        if (!userInitiated && !AutoUpdate) return;
        checking = true;
        if (userInitiated) StatusText = "Memeriksa update…";
        try
        {
            RemoveOldDownloads();
            using var http = UpdateChecker.CreateClient(CurrentVersion);
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
            var latest = await UpdateChecker.GetLatestAsync(http, cts.Token);
            if (latest is null || !UpdateChecker.IsNewer(latest.Version, CurrentVersion))
            {
                StatusText = userInitiated ? $"EVSEQ sudah versi terbaru ({CurrentVersionText})." : "";
                return;
            }

            StatusText = $"Mengunduh update v{latest.Version}…";
            installerPath = await UpdateChecker.DownloadInstallerAsync(http, latest, downloadFolder, cts.Token);
            ReadyVersion = latest.Version.ToString();
            IsReady = BannerVisible = true;
            StatusText = $"Update v{ReadyVersion} siap — dipasang saat EVSEQ ditutup.";
            main.Log($"Update v{ReadyVersion} sudah diunduh dan diverifikasi; dipasang saat EVSEQ ditutup", ActivityKind.Success);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException
                                       or InvalidDataException or JsonException or UnauthorizedAccessException)
        {
            // Offline or GitHub unreachable is normal on a show laptop: only report when asked.
            StatusText = userInitiated ? $"Tidak bisa memeriksa update: {ex.Message}" : "";
            if (ex is InvalidDataException) main.Log(ex.Message, ActivityKind.Warning); // e.g. checksum mismatch
        }
        finally
        {
            checking = false;
        }
    }

    /// <summary>
    /// Starts the verified installer in update mode (silent, with a progress window). Called after the
    /// main window has closed. Returns false when there is nothing to install or the user declined UAC.
    /// </summary>
    public bool LaunchInstaller(bool relaunchAfterwards)
    {
        if (!IsReady || installerPath is null || !File.Exists(installerPath)) return false;
        try
        {
            var args = "/SILENT /SUPPRESSMSGBOXES /NORESTART" + (relaunchAfterwards ? " /RELAUNCH=1" : "");
            Process.Start(new ProcessStartInfo(installerPath, args) { UseShellExecute = true });
            return true;
        }
        catch (Win32Exception)
        {
            return false; // UAC declined: the verified download stays and is offered again next time
        }
    }

    /// <summary>Installers for this version or older are no longer needed.</summary>
    private void RemoveOldDownloads()
    {
        if (!Directory.Exists(downloadFolder)) return;
        foreach (var file in Directory.GetFiles(downloadFolder, "EVSEQ-Setup-*"))
        {
            var name = Path.GetFileNameWithoutExtension(file).Replace("EVSEQ-Setup-", "").Replace(".exe", "");
            if (UpdateChecker.ParseTag(name) is { } v && !UpdateChecker.IsNewer(v, CurrentVersion))
            {
                try { File.Delete(file); } catch (IOException) { /* in use; next time */ }
            }
        }
    }
}
