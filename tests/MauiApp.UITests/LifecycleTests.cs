using OpenQA.Selenium;
using NdiForAndroid.UITests.Infrastructure;
using NdiForAndroid.UITests.Pages;
using Xunit;

namespace NdiForAndroid.UITests;

/// <summary>
/// Interruptions against the deep-linked viewer's permanent "Connecting..." hold state. No real
/// NDI stream exists on the CI emulator, so "active" here means this hold state, not actual
/// playback — see the plan's baseline notes for why the state never times out on its own and is
/// therefore a stable anchor for these tests. Since #413 an initial connect ends after ~15 s with
/// "Could not connect to the source.", but only while the bridge holds a receiver in Connecting;
/// the x86_64 CI emulator has no NDI runtime, so no receiver is ever created there and the hold
/// state still never ends. On an ARM device with the runtime it ends ~15 s after the deep link,
/// so run these against the emulator.
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

        // On a compact (phone-class) device, landscape while playing enters full screen in place
        // (YouTube-style full screen): the deck/sheet controls and the status label give way to the
        // auto-hiding overlay, so the viewer page being present is the "alive" signal in landscape;
        // the status text is asserted again once portrait has exited full screen.
        app.Rotate(ScreenOrientation.Landscape);
        Assert.True(app.Viewer.IsVisible, "Viewer left the screen after rotating to landscape");

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

    /// <summary>
    /// #450: Back from a pushed viewer must dispose the page's Transient ViewModel, which is what
    /// hands the receiver back. Shell raises Disappearing before the page leaves the nav stack,
    /// so the old stack check in OnDisappearing never fired on a pop and the ViewModel leaked; the
    /// fix disposes from OnNavigatedFrom. No NDI runtime on the x86 emulator, so the bridge state
    /// cannot be asserted here — the NDI-Nav trace is the observable: it is only written in
    /// developer mode, hence the Settings round trip.
    /// </summary>
    [SkippableFact]
    public void BackFromAPushedViewer_DisposesTheViewerAndHandsTheReceiverBack() => RunWithCrashGate(app =>
    {
        SetDeveloperMode(app, on: true);
        try
        {
            // Clear logcat first so a trace left by an earlier test cannot satisfy the assertions.
            app.ShellCommand("logcat", "-c");
            EnterHoldState(app);

            app.PressBackButton();

            // The deep link lands on //view and pushes the viewer onto its stack, so Back returns
            // to the source list.
            app.Sources.WaitUntilVisible(Timeouts.Navigation);
            Assert.False(app.Viewer.IsVisible, "The viewer page is still on screen after Back");

            var nav = app.ShellCommand("logcat", "-d", "-s", "NDI-Nav");
            var afterPop = TraceAfterLastPop(nav);

            Assert.Equal(1, CountLines(afterPop, "viewer.navigatedfrom type="));
            Assert.Contains("offStack=True", afterPop);
            Assert.Equal(1, CountLines(afterPop, "viewer.teardown"));
            Assert.Equal(1, CountLines(afterPop, "viewer.vm.dispose.end"));
            Assert.DoesNotContain("viewer.navigatedfrom.mismatch", afterPop);
        }
        finally
        {
            // Developer mode is persisted; leave it as the suite found it, then let the next test
            // relaunch fresh — tapping the already-selected tab would not pop a pushed page anyway.
            SetDeveloperMode(app, on: false);
            app.Terminate();
        }
    });

    private static void SetDeveloperMode(NdiApp app, bool on)
    {
        app.ResetToHome();
        app.Navigation.GoTo(NavDestination.Settings);
        app.Settings.WaitUntilVisible();
        app.Settings.OpenSection(SettingsSection.DeveloperTools);

        if (app.Settings.IsDeveloperModeOn != on)
        {
            app.Settings.ToggleDeveloperModeViaLabel();
            Assert.Equal(on, app.Settings.IsDeveloperModeOn);
        }
    }

    /// <summary>
    /// The NDI-Nav lines from the last <c>shell.navigating.begin</c> whose <c>src=</c> is a pop
    /// (<c>Pop</c> or <c>PopToRoot</c>) to the end of the dump — Back is the only pop this test
    /// makes, so everything the fix emits sits after it. Fails loudly when no pop was traced at
    /// all: that would mean developer mode did not take or Back never navigated.
    /// </summary>
    private static string TraceAfterLastPop(string navLog)
    {
        var lines = navLog.Split('\n');
        var lastPop = Array.FindLastIndex(lines, line =>
            line.Contains("shell.navigating.begin", StringComparison.Ordinal)
            && (line.Contains("src=Pop ", StringComparison.Ordinal)
                || line.Contains("src=PopToRoot ", StringComparison.Ordinal)));

        Assert.True(lastPop >= 0,
            "No `shell.navigating.begin … src=Pop|PopToRoot` line in the NDI-Nav trace after Back. Dump:\n" + navLog);

        return string.Join('\n', lines.Skip(lastPop));
    }

    private static int CountLines(string text, string marker) =>
        text.Split('\n').Count(line => line.Contains(marker, StringComparison.Ordinal));
}
