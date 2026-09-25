using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace AudioPlayer.Core.Audio;

public sealed record AudioDevice(string Id, string Name);

public sealed class AudioDevices : IDisposable
{
    private readonly MMDeviceEnumerator enumerator = new();
    private readonly IDisposable? notifications;

    public AudioDevices()
    {
        var n = enumerator.CreateNotificationClient();
        n.DeviceStateChanged += (_, e) =>
        {
            if (e.NewState != DeviceState.Active) DeviceUnavailable?.Invoke(e.DeviceId);
        };
        notifications = n as IDisposable;
    }

    /// <summary>Raised with the device id when a render device is unplugged/disabled.</summary>
    public event Action<string>? DeviceUnavailable;

    public IReadOnlyList<AudioDevice> RenderDevices() =>
        enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active)
            .Select(d => new AudioDevice(d.ID, d.FriendlyName))
            .ToList();

    public MMDevice? Find(string? id)
    {
        if (id is null) return null;
        try
        {
            var d = enumerator.GetDevice(id);
            return d.State == DeviceState.Active ? d : null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    public MMDevice Default() => enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);

    public void Dispose()
    {
        notifications?.Dispose();
        enumerator.Dispose();
    }
}
