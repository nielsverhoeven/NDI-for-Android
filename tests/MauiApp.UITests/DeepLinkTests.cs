using NdiForAndroid.UITests.Infrastructure;
using Xunit;

namespace NdiForAndroid.UITests;

/// <summary>
/// Deep-link entry points: cold start, warm start (<c>OnNewIntent</c>), and the malformed/unknown
/// forms that must surface an error rather than crash. No NDI source exists on the CI network, so
/// every link here targets a source id that cannot resolve to a real one — the point is navigation
/// and error handling, not an actual connection.
/// </summary>
[Collection("AppiumSession")]
public sealed class DeepLinkTests : DeviceInterruptionTestBase
{
    private const string UnknownSourceId = "e2e-deeplink-unknown-source";

    public DeepLinkTests(AppiumDriverFixture fixture) : base(fixture) { }

    [SkippableFact]
    public void ColdStart_ViewLink_NavigatesToTheViewer() => RunWithCrashGate(app =>
    {
        app.Terminate();
        app.DeepLink($"ndi://view?sourceId={UnknownSourceId}");

        app.Viewer.WaitUntilVisible(Timeouts.AppStart);
        app.Viewer.WaitUntilPlaying();
        Assert.Equal("Connecting...", app.Viewer.Status);

        // "viewer" is a relative Shell route pushed onto the current tab's stack, so tapping the
        // already-selected Home tab does not pop back to it. Terminate and let the next test's
        // EnsureInForeground relaunch fresh onto Home instead of navigating back.
        app.Terminate();
    });

    [SkippableFact]
    public void WarmStart_ViewLink_NavigatesViaOnNewIntent() => RunWithCrashGate(app =>
    {
        app.ResetToHome();
        app.DeepLink($"ndi://view?sourceId={UnknownSourceId}");

        app.Viewer.WaitUntilVisible(Timeouts.Navigation);
        app.Viewer.WaitUntilPlaying();
        Assert.Equal("Connecting...", app.Viewer.Status);

        app.Terminate();
    });

    [SkippableFact]
    public void ColdStart_StreamLink_NavigatesToOutputInReStreamMode() => RunWithCrashGate(app =>
    {
        app.Terminate();
        app.DeepLink($"ndi://stream?sourceId={UnknownSourceId}");

        app.Output.WaitUntilVisible(Timeouts.AppStart);

        // IsReStreamMode is a plain property read (no built-in wait) and the query-param bound
        // ViewModel state can lag a beat behind the page itself becoming visible.
        var deadline = DateTime.UtcNow + Timeouts.Navigation;
        while (DateTime.UtcNow < deadline && !app.Output.IsReStreamMode)
            Thread.Sleep(250);

        Assert.True(app.Output.IsReStreamMode, "Stream deep link did not switch Output into re-stream mode");

        // OutputViewModel is a Singleton: re-stream mode set here survives for the rest of the
        // process and would otherwise follow every later test in this collection onto the Stream tab.
        if (app.Output.IsReStreamMode)
            app.Output.ToggleReStreamMode();

        app.ResetToHome();
    });

    [SkippableFact]
    public void MalformedLink_MissingSourceId_ShowsAnErrorAndDoesNotNavigate() => RunWithCrashGate(app =>
    {
        app.ResetToHome();
        app.DeepLink("ndi://view");

        Assert.True(app.HasVisibleToast("Invalid deep link"), "No error toast appeared for a deep link with no sourceId");
        Assert.True(app.Home.IsVisible, "A malformed deep link left the app off Home");
    });

    [SkippableFact]
    public void MalformedLink_UnknownAction_ShowsAnErrorAndDoesNotNavigate() => RunWithCrashGate(app =>
    {
        app.ResetToHome();
        app.DeepLink($"ndi://record?sourceId={UnknownSourceId}");

        Assert.True(app.HasVisibleToast("Unknown action"), "No error toast appeared for an unknown-action deep link");
        Assert.True(app.Home.IsVisible, "An unknown-action deep link left the app off Home");
    });
}
