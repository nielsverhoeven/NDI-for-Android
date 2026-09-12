using OpenQA.Selenium;
using NdiForAndroid.UITests.Infrastructure;
using NdiForAndroid.UITests.Pages;
using Xunit;

namespace NdiForAndroid.UITests;

/// <summary>
/// Interruptions against the deep-linked viewer's permanent "Connecting..." hold state. No real
/// NDI stream exists on the CI emulator, so "active" here means this hold state, not actual
/// playback — see the plan's baseline notes for why the state never times out on its own and is
/// therefore a stable anchor for these tests.
/// </summary>
[Collection("AppiumSession")]
public sealed class LifecycleTests : DeviceInterruptionTestBase
{
    private const string UnknownSourceId = "e2e-lifecycle-hold";

    public LifecycleTests(AppiumDriverFixture fixture) : base(fixture) { }

    private static void EnterHoldState(NdiApp app)
    {
        app.ResetToHome();
        app.DeepLink($"ndi://view?sourceId={UnknownSourceId}");
        app.Viewer.WaitUntilVisible(Timeouts.Navigation);
        app.Viewer.WaitUntilPlaying();
    }

    [SkippableFact]
    public void Rotation_BothWays_KeepsTheViewerAliveAndPlaying() => RunWithCrashGate(app =>
    {
        EnterHoldState(app);

        // Landscape lays the viewer out as a collapsed bottom sheet on this device profile, so
        // viewer.stop (IsPlaying's signal) is not Displayed there — the status text is the one
        // signal that holds across both placements.
        app.Rotate(ScreenOrientation.Landscape);
        Assert.True(app.Viewer.IsVisible, "Viewer left the screen after rotating to landscape");
        Assert.Equal("Connecting...", app.Viewer.Status);

        app.Rotate(ScreenOrientation.Portrait);
        Assert.True(app.Viewer.IsVisible, "Viewer left the screen after rotating back to portrait");
        Assert.Equal("Connecting...", app.Viewer.Status);

        // "viewer" is a relative Shell route pushed onto the current tab's stack, so tapping the
        // already-selected Home tab does not pop back to it. Terminate and let the next test's
        // EnsureInForeground relaunch fresh onto Home instead of navigating back.
        app.Terminate();
    });

    [SkippableFact]
    public void BackgroundAndForeground_KeepsTheViewerAliveAndPlaying() => RunWithCrashGate(app =>
    {
        EnterHoldState(app);

        app.SendToBackground();
        Thread.Sleep(TimeSpan.FromSeconds(3));

        app.EnsureInForeground();
        app.Viewer.WaitUntilVisible(Timeouts.Navigation);
        Assert.True(app.Viewer.IsPlaying, "Viewer did not resume playing after returning to the foreground");

        app.Terminate();
    });

    [SkippableFact]
    public void ProcessDeath_AmKill_RelaunchesToASaneState() => RunWithCrashGate(app =>
    {
        EnterHoldState(app);

        // `am kill` only reaps a cached/background process — background the app first so the kill
        // is not a no-op against a foreground activity the OS refuses to touch.
        app.SendToBackground();
        Thread.Sleep(TimeSpan.FromSeconds(2));
        app.ShellCommand("am", "kill", NdiApp.PackageName);

        // The process is gone; there is no state to resume — this is a fresh cold start, same as a
        // user relaunching from the launcher after the OS reclaimed memory in the background.
        Assert.True(app.TryRestart(), "The app did not come back to the foreground after am kill");
        app.Home.WaitUntilVisible(Timeouts.AppStart);
    });

    [SkippableFact]
    public void IncomingCall_DuringHold_DoesNotCrashAndRemainsRecoverable() => RunWithCrashGate(app =>
    {
        EnterHoldState(app);

        const string callerNumber = "5551234567";
        app.GsmCall(callerNumber, "call");
        Thread.Sleep(TimeSpan.FromSeconds(2));

        // Cancel regardless of what took the foreground (our app, a system dialer, or nothing) —
        // "cancel" is safe to issue even if the call was never answered.
        app.GsmCall(callerNumber, "cancel");

        app.EnsureInForeground();
        app.Viewer.WaitUntilVisible(Timeouts.Navigation);

        app.Terminate();
    });
}
