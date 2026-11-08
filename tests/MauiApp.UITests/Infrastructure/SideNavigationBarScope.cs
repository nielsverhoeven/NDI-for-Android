using System.Text.RegularExpressions;
using OpenQA.Selenium;
using NdiForAndroid.UITests.Pages;
using Xunit;
using Xunit.Abstractions;

namespace NdiForAndroid.UITests.Infrastructure;

/// <summary>
/// Puts the device into the one configuration that can make the navigation bar cover the left
/// rail: 3-button navigation, rotation locked to <c>ROTATION_270</c> (landscape with the bar on
/// the left edge). Restores the device on dispose.
/// </summary>
/// <remarks>
/// <para>
/// <b>Test-scoped on purpose.</b> The architect's 2026-10-20 ruling (#321, item 7) forbids making
/// this a device-wide setting in <c>run-emulator-tests.sh</c>: every other test would then run
/// under a side bar, and a guard without a fix would turn the whole release gate red. So the
/// setup lives here, is entered by the tests that need it, and is undone before the next test.
/// </para>
/// <para>
/// <b>Skips, never fails, when the device cannot do it.</b> An AVD without the 3-button overlay,
/// or an Appium server started without <c>--allow-insecure=uiautomator2:adb_shell</c>, is an
/// environment that cannot answer the question — not a product defect — so <see cref="Enter"/>
/// throws xUnit's skip exception. The CI runner starts Appium with that flag already (it is what
/// the deep-link, permission and lifecycle tests use).
/// </para>
/// <para>
/// <b>Why not <see cref="NdiApp.Rotate"/>.</b> Appium's <c>LANDSCAPE</c> is whichever landscape
/// rotation UiAutomator picks, and Android docks a movable navigation bar on the <em>right</em> for
/// <c>ROTATION_90</c> and on the <em>left</em> for <c>ROTATION_270</c>
/// (<c>DisplayPolicy.navigationBarPosition</c>). Only the left-docked case can overlap the rail,
/// so the rotation is set explicitly through the same two settings UiAutomator's own freeze
/// uses: <c>accelerometer_rotation 0</c> and <c>user_rotation 3</c>.
/// </para>
/// <para>
/// Tests that enter this scope carry <c>[Trait("Category", "SideNavigationBar")]</c>
/// (<see cref="TraitCategory"/>). The default CI run excludes that category
/// (<c>run-emulator-tests.sh</c> applies <c>Category!=SideNavigationBar</c> when no filter is
/// given), so the guard is dispatched on demand with a <c>test_filter</c> until a fix exists and
/// the architect lifts the deferral.
/// </para>
/// </remarks>
public sealed class SideNavigationBarScope : IDisposable
{
    /// <summary>Trait value shared by every test that needs the side navigation bar.</summary>
    public const string TraitCategory = "SideNavigationBar";

    private const string ThreeButtonOverlay = "com.android.internal.systemui.navbar.threebutton";
    private const string NavBarOverlayPrefix = "com.android.internal.systemui.navbar.";

    private static readonly Regex OverlayLine = new(@"^\s*\[(?<on>[x ])\]\s+(?<name>\S+)", RegexOptions.Multiline);

    private readonly NdiApp _app;
    private readonly ITestOutputHelper _output;
    private readonly string? _previousOverlay;
    private readonly string _previousAccelerometerRotation;
    private readonly string _previousUserRotation;
    private bool _disposed;

    private SideNavigationBarScope(
        NdiApp app, ITestOutputHelper output,
        string? previousOverlay, string previousAccelerometerRotation, string previousUserRotation)
    {
        _app = app;
        _output = output;
        _previousOverlay = previousOverlay;
        _previousAccelerometerRotation = previousAccelerometerRotation;
        _previousUserRotation = previousUserRotation;
    }

    /// <summary>The navigation bar as it was reported once the device had settled.</summary>
    public SystemBarBounds NavigationBar { get; private set; }

    /// <summary>
    /// Enables 3-button navigation, locks the rotation to <c>ROTATION_270</c>, and waits for the
    /// app to be laid out in landscape with the navigation bar reported.
    /// </summary>
    /// <exception cref="SkipException">
    /// The device has no 3-button overlay, or shell access is not available to the test.
    /// </exception>
    public static SideNavigationBarScope Enter(NdiApp app, ITestOutputHelper output)
    {
        string overlays;
        try
        {
            overlays = app.ShellCommand("cmd", "overlay", "list");
        }
        catch (InvalidOperationException ex)
        {
            throw new SkipException(
                "The side-navigation-bar setup needs `mobile: shell` (Appium started with " +
                $"--allow-insecure=uiautomator2:adb_shell); it is not available here: {ex.Message}");
        }

        var navBarOverlays = NavBarOverlays(overlays).ToList();
        if (navBarOverlays.All(o => o.Name != ThreeButtonOverlay))
            throw new SkipException(
                $"This device has no '{ThreeButtonOverlay}' overlay, so 3-button navigation cannot be " +
                "enabled and a side navigation bar cannot be produced. Navigation overlays present: " +
                $"[{string.Join(", ", navBarOverlays.Select(o => o.Name))}].");

        var previousOverlay = navBarOverlays.FirstOrDefault(o => o.Enabled).Name;
        var previousAccelerometer = app.ShellCommand("settings", "get", "system", "accelerometer_rotation").Trim();
        var previousUserRotation = app.ShellCommand("settings", "get", "system", "user_rotation").Trim();

        output.WriteLine(
            $"side-nav-bar setup: previous overlay={previousOverlay ?? "(none)"}, " +
            $"accelerometer_rotation={previousAccelerometer}, user_rotation={previousUserRotation}");

        var scope = new SideNavigationBarScope(app, output, previousOverlay, previousAccelerometer, previousUserRotation);

        try
        {
            // enable-exclusive turns every other overlay in the category (gestural, two-button) off
            // in the same step, which is what SystemUI's own navigation-mode switch does.
            app.ShellCommand("cmd", "overlay", "enable-exclusive", "--category", ThreeButtonOverlay);

            var after = NavBarOverlays(app.ShellCommand("cmd", "overlay", "list")).ToList();
            if (!after.Any(o => o.Name == ThreeButtonOverlay && o.Enabled))
                throw new SkipException(
                    $"Enabling '{ThreeButtonOverlay}' did not take: the overlay list afterwards is " +
                    $"[{string.Join(", ", after.Select(o => $"{(o.Enabled ? "[x]" : "[ ]")} {o.Name}"))}].");

            app.ShellCommand("settings", "put", "system", "accelerometer_rotation", "0");
            app.ShellCommand("settings", "put", "system", "user_rotation", "3");

            scope.WaitUntilSettled();
            return scope;
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }

    private void WaitUntilSettled()
    {
        // Rotation and the navigation-mode switch both re-lay out the window; give them the same
        // settle budget a plain Appium rotation gets, then poll for the state we asked for.
        Thread.Sleep(Timeouts.OrientationSettle);

        var deadline = DateTime.UtcNow + Timeouts.Navigation;
        var landscape = false;
        var barVisible = false;
        while (DateTime.UtcNow < deadline)
        {
            landscape = SafeOrientation() == ScreenOrientation.Landscape;
            NavigationBar = _app.Metrics.NavigationBar;
            barVisible = NavigationBar.Visible;

            if (landscape && barVisible && _app.IsInForeground)
                break;

            Thread.Sleep(250);
        }

        var rotation = _app.ShellCommand("settings", "get", "system", "user_rotation").Trim();
        _output.WriteLine(
            $"side-nav-bar setup: user_rotation={rotation} (3 = ROTATION_270, bar on the left), " +
            $"orientation={SafeOrientation()}, window={_app.WindowSize.Width}x{_app.WindowSize.Height}, " +
            $"navigation bar {NavigationBar}, status bar height {_app.Metrics.StatusBarHeight}px");

        if (!_app.IsInForeground)
            throw new InvalidOperationException(
                "The app left the foreground while switching to 3-button navigation under ROTATION_270 — " +
                $"'{_app.ForegroundPackage}' is in front. Nothing has been tapped yet, so this is the " +
                "configuration change itself bringing the app down.");

        if (!landscape)
            throw new InvalidOperationException(
                "user_rotation=3 with accelerometer_rotation=0 did not put the device in landscape " +
                $"within {Timeouts.Navigation.TotalSeconds:0}s (orientation is {SafeOrientation()}). " +
                "The side-navigation-bar measurement needs ROTATION_270.");

        if (!barVisible)
            throw new InvalidOperationException(
                "The navigation bar is reported as not visible after enabling 3-button navigation; the " +
                $"overlap measurement needs a visible bar (got {NavigationBar}).");
    }

    private ScreenOrientation? SafeOrientation()
    {
        try { return _app.Orientation; }
        catch { return null; }
    }

    private static IEnumerable<(string Name, bool Enabled)> NavBarOverlays(string overlayList) =>
        OverlayLine.Matches(overlayList)
            .Select(m => (Name: m.Groups["name"].Value, Enabled: m.Groups["on"].Value == "x"))
            .Where(o => o.Name.StartsWith(NavBarOverlayPrefix, StringComparison.Ordinal));

    /// <summary>
    /// Restores the previous navigation mode and leaves the device in portrait, which is what
    /// every other test in the collection assumes as its starting point.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        try
        {
            if (_previousOverlay is not null)
                _app.ShellCommand("cmd", "overlay", "enable-exclusive", "--category", _previousOverlay);
            else
                _app.ShellCommand("cmd", "overlay", "disable", ThreeButtonOverlay);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"side-nav-bar teardown: could not restore the navigation overlay: {ex.Message}");
        }

        try
        {
            _app.ShellCommand("settings", "put", "system", "user_rotation", NormalizeSetting(_previousUserRotation, "0"));
            _app.ShellCommand("settings", "put", "system", "accelerometer_rotation", NormalizeSetting(_previousAccelerometerRotation, "1"));
        }
        catch (Exception ex)
        {
            _output.WriteLine($"side-nav-bar teardown: could not restore the rotation settings: {ex.Message}");
        }

        // Through Appium, like every other test's finally block, so the collection's shared
        // session sees the same frozen-portrait state it always leaves behind.
        try
        {
            _app.Rotate(ScreenOrientation.Portrait);
        }
        catch (Exception ex)
        {
            _output.WriteLine($"side-nav-bar teardown: could not rotate back to portrait: {ex.Message}");
        }
    }

    // `settings get` prints "null" for a key that was never written.
    private static string NormalizeSetting(string value, string fallback) =>
        string.IsNullOrWhiteSpace(value) || value == "null" ? fallback : value;
}
