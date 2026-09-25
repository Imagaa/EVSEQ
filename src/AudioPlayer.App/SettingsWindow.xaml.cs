using System.Windows;
using AudioPlayer.App.ViewModels;
using AudioPlayer.Core.Audio;
using AudioPlayer.Core.Model;

namespace AudioPlayer.App;

public partial class SettingsWindow : Window
{
    private readonly MainViewModel vm;

    public SettingsWindow(MainViewModel vm)
    {
        InitializeComponent();
        this.vm = vm;

        var devices = vm.Devices.RenderDevices();
        MainDevice.ItemsSource = devices;
        MonitorDevice.ItemsSource = devices;
        MainDevice.SelectedValue = vm.Engine.Main.DeviceId;
        MonitorDevice.SelectedValue = vm.Project.MonitorDeviceId;

        var fade = vm.Project.DefaultFade;
        FadeIn.Text = fade.FadeInMs.ToString();
        FadeOut.Text = fade.FadeOutMs.ToString();
        Curve.ItemsSource = Enum.GetValues<FadeCurve>();
        Curve.SelectedItem = fade.Curve;
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (!TryMs(FadeIn.Text, out var fadeIn) || !TryMs(FadeOut.Text, out var fadeOut))
        {
            MessageBox.Show(this, "Durasi fade harus angka 0–10000 ms.", "Settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var mainId = (string?)MainDevice.SelectedValue;
        var monitorId = (string?)MonitorDevice.SelectedValue;
        if (mainId is not null && mainId == monitorId &&
            MessageBox.Show(this, "Main dan Monitor memakai device yang sama — preview akan terdengar oleh audiens. Lanjut?",
                "Peringatan", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        vm.ApplySettings(mainId, monitorId,
            new FadeSettings { FadeInMs = fadeIn, FadeOutMs = fadeOut, Curve = (FadeCurve)Curve.SelectedItem });
        DialogResult = true;
    }

    private static bool TryMs(string text, out int ms) => int.TryParse(text, out ms) && ms is >= 0 and <= 10000;
}
