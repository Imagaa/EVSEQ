using AudioPlayer.Core.Model;

namespace AudioPlayer.Core.Tests;

public class SessionRecoveryTests
{
    private readonly string dir = Path.Combine(Path.GetTempPath(), $"ap-rec-{Guid.NewGuid():N}");

    private static Project OneTrack() => new() { Tracks = [new Track { FilePath = @"C:\a.wav", Title = "A" }] };

    [Fact]
    public void FirstRunHasNothingToRecover() => Assert.False(new SessionRecovery(dir).PreviousSessionCrashed);

    [Fact]
    public void CleanExitLeavesNothingToRecover()
    {
        var r = new SessionRecovery(dir);
        r.BeginSession();
        r.Save(OneTrack(), null);
        r.EndSession();
        Assert.False(new SessionRecovery(dir).PreviousSessionCrashed);
    }

    [Fact]
    public void CrashLeavesRecoverableProjectAndOriginalPath()
    {
        var r = new SessionRecovery(dir);
        r.BeginSession();
        r.Save(OneTrack(), @"D:\shows\wedding.approj");
        // no EndSession: simulated crash

        var next = new SessionRecovery(dir);
        Assert.True(next.PreviousSessionCrashed);
        var (project, original) = next.Restore();
        Assert.Equal("A", Assert.Single(project.Tracks).Title);
        Assert.Equal(@"D:\shows\wedding.approj", original);
    }

    [Fact]
    public void CrashWithoutUnsavedChangesOffersNothing()
    {
        var r = new SessionRecovery(dir);
        r.BeginSession();
        r.Save(OneTrack(), null);
        r.DiscardAutosave(); // user saved; nothing pending
        Assert.False(new SessionRecovery(dir).PreviousSessionCrashed);
    }
}
