using OpenQA.Selenium;
using NdiForAndroid.UITests.Infrastructure;
using NdiForAndroid.UITests.Pages;
using Xunit;
using Xunit.Abstractions;

namespace NdiForAndroid.UITests;

/// <summary>
/// Exiting full screen on a compact device while the device itself stays in landscape.
/// </summary>
/// <remarks>
/// <para>
/// The viewer's invariant on a phone is "full screen if and only if landscape while playing". The
/// exit button requests portrait, and once the portrait arrival has taken the viewer out of full
/// screen the orientation request is released. A device that is still physically (or, with
/// auto-rotate off, by setting) in landscape then rotates the window straight back to landscape,
/// and the landscape arrival re-enters full screen.
/// </para>
/// <para>
/// The deterministic variant needs a stable "device stays landscape" premise: auto-rotate off and
/// the user rotation locked to landscape, which is what Appium's <c>Rotate</c> leaves behind. The
/// premise is read back from the device and treated as a skip, never an assertion — an emulator
/// that does not hold it says nothing about the behaviour under test, and failing on it would
/// look like a reproduction of the bug.
/// </para>
/// <para>
/// No NDI runtime exists on the CI emulator, so the deep-linked source stays in its permanent
/// "Connecting..." hold state, which the viewer treats as playing (see <see cref="LifecycleTests"/>).
/// </para>
/// </remarks>
[Collection("AppiumSession")]
public sealed class FullScreenExitTests : UiTestBase
{
    private const string HoldSourceId = "e2e-fullscreen-exit";

    private readonly ITestOutputHelper _output;

    public FullScreenExitTests(AppiumDriverFixture fixture, ITestOutputHelper output) : base(fixture) =>
        _output = output;

    [SkippableFact]
    public void ExitFromLandscapeFullScreen_StaysOutWhileTheDeviceStaysLandscape() => Run(app =>
    {
        try
        {
            app.ResetToHome();

            var smallestWidthDp = app.Metrics.SmallestWidthDp;
            Skip.If(smallestWidthDp >= 600,
                "Rotation-driven full screen is compact-device-only; this device reports " +
                $"sw={smallestWidthDp:0}dp, i.e. tablet-class.");

            app.DeepLink($"ndi://view?sourceId={HoldSourceId}");
            app.Viewer.WaitUntilVisible(Timeouts.Navigation);
            app.Viewer.WaitUntilPlaying();

            app.Rotate(ScreenOrientation.Landscape);

            var autoRotate = ReadSystemSetting(app, "accelerometer_rotation");
            var userRotation = ReadSystemSetting(app, "user_rotation");
            _output.WriteLine($"accelerometer_rotation='{autoRotate}' user_rotation='{userRotation}'");

            Skip.IfNot(autoRotate == "0" && (userRotation is "1" or "3"),
                "Premise not held: the device must have auto-rotate off (accelerometer_rotation=0) and " +
                $"the user rotation locked to landscape (user_rotation 1 or 3). Read " +
                $"accelerometer_rotation='{autoRotate}' user_rotation='{userRotation}'.");

            app.Viewer.WaitUntilFullScreen();

            app.Viewer.ExitFullScreen();
            Thread.Sleep(TimeSpan.FromSeconds(5));

            Assert.False(app.Viewer.IsFullScreen,
                "#384: exit bounced back into full screen while the device stayed landscape");
            Assert.True(app.Viewer.IsPlaying,
                "The viewer stopped playing after exiting full screen while the device stayed landscape");
        }
        finally
        {
            try { app.Rotate(ScreenOrientation.Portrait); } catch { }
            try { app.Terminate(); } catch { }
        }
    });

    private static string ReadSystemSetting(NdiApp app, string name) =>
        app.ShellCommand("settings", "get", "system", name).Trim();
}
