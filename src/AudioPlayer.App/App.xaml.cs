using System.Windows;

namespace AudioPlayer.App;

public partial class App : Application
{
    // One instance only: a second one would fight over global hotkeys and the autosave/session files.
    private static Mutex? singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        singleInstance = new Mutex(initiallyOwned: true, @"Local\AudioPlayer.SingleInstance", out bool isFirst);
        if (!isFirst)
        {
            MessageBox.Show("EVSEQ sudah berjalan.", "Event Sequencer", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }
        base.OnStartup(e);
        new MainWindow().Show();
    }
}
