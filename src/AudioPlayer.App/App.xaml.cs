using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Windows;

namespace AudioPlayer.App;

public partial class App : Application
{
    // One instance only: a second one would fight over global hotkeys and the autosave/session files.
    // (Mutex name kept from before the rename: the installer waits on it.)
    private static Mutex? singleInstance;

    // A second launch (e.g. double-clicking a .approj while EVSEQ runs) hands its project to the running one.
    private static readonly string OpenPipeName = $"EVSEQ.OpenProject.{Process.GetCurrentProcess().SessionId}";

    protected override void OnStartup(StartupEventArgs e)
    {
        // EVSEQ.exe "show.approj" opens that project (file association, shortcuts).
        var project = e.Args.FirstOrDefault(a => a.EndsWith(".approj", StringComparison.OrdinalIgnoreCase));

        singleInstance = new Mutex(initiallyOwned: true, @"Local\AudioPlayer.SingleInstance", out bool isFirst);
        if (!isFirst)
        {
            if (project is null || !SendToRunningInstance(Path.GetFullPath(project)))
                MessageBox.Show("EVSEQ sudah berjalan.", "Event Sequencer", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        base.OnStartup(e);
        var window = new MainWindow(project);
        window.Show();
        _ = ListenForProjectsAsync(window);
    }

    private static bool SendToRunningInstance(string path)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", OpenPipeName, PipeDirection.Out, PipeOptions.CurrentUserOnly);
            client.Connect(3000);
            using var writer = new StreamWriter(client);
            writer.Write(path);
            return true;
        }
        catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Receives project paths from later launches for as long as EVSEQ runs.</summary>
    private static async Task ListenForProjectsAsync(MainWindow window)
    {
        while (true)
        {
            try
            {
                await using var server = new NamedPipeServerStream(OpenPipeName, PipeDirection.In, 1,
                    PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await server.WaitForConnectionAsync();
                using var reader = new StreamReader(server);
                var path = await reader.ReadToEndAsync();
                if (path.EndsWith(".approj", StringComparison.OrdinalIgnoreCase))
                    window.OpenFromShell(path); // continuation runs on the UI thread
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                await Task.Delay(500); // a broken connection must not stop listening
            }
        }
    }
}
