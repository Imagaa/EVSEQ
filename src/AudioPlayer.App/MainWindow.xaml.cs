using System.Windows;
using AudioPlayer.App.ViewModels;
using Microsoft.Win32;

namespace AudioPlayer.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = vm;
        Closed += (_, _) => vm.Dispose();
    }

    private void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "Audio|*.wav;*.mp3;*.aif;*.aiff;*.flac|Semua file|*.*",
        };
        if (dlg.ShowDialog(this) == true) vm.AddFiles(dlg.FileNames);
    }
}
