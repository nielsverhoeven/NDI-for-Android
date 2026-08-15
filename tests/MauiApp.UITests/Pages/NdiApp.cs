using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;
using NdiForAndroid.UITests.Infrastructure;

namespace NdiForAndroid.UITests.Pages;

/// <summary>
/// Entry point to the page objects, plus the device-level actions tests need.
/// </summary>
/// <remarks>
/// Tests hold one of these rather than a driver. That is what keeps locators out of test methods:
/// there is no <c>FindElement</c> on the surface a test can see.
/// </remarks>
public sealed class NdiApp
{
    /// <summary>The app's Android package — used for lifecycle commands.</summary>
    public const string PackageName = "com.ndi.android";

    private readonly AndroidDriver _driver;

    public NdiApp(AndroidDriver driver)
    {
        _driver = driver;

        Navigation    = new NavigationBar(driver);
        Home          = new HomePage(driver);
        Sources       = new SourceListPage(driver);
        Output        = new OutputPage(driver);
        Viewer        = new ViewerPage(driver);
        Settings      = new SettingsPage(driver);
        DiagnosticLog = new DiagnosticLogPage(driver);
    }

    public NavigationBar      Navigation    { get; }
    public HomePage           Home          { get; }
    public SourceListPage     Sources       { get; }
    public OutputPage         Output        { get; }
    public ViewerPage         Viewer        { get; }
    public SettingsPage       Settings      { get; }
    public DiagnosticLogPage  DiagnosticLog { get; }

    /// <summary>Current screen orientation.</summary>
    public ScreenOrientation Orientation => _driver.Orientation;

    /// <summary>Device geometry: system-bar insets and display density.</summary>
    public DeviceMetrics Metrics => new(_driver);

    /// <summary>
    /// Takes a screenshot for pixel sampling.
    /// </summary>
    /// <remarks>
    /// The one capability that cannot be expressed as a page object. Colour is absent from the
    /// accessibility tree — a MAUI <c>Path</c>'s <c>Fill</c> is simply not there — so questions
    /// like "is this icon visible against its background" have to be answered from pixels. Caller
    /// disposes.
    /// </remarks>
    public ScreenSampler CaptureScreen() => ScreenSampler.Capture(_driver);

    /// <summary>Audits the live accessibility tree.</summary>
    public AccessibilityAudit Accessibility => new(_driver, Metrics);

    /// <summary>Window size in pixels — the reference frame for placement assertions.</summary>
    public System.Drawing.Size WindowSize => _driver.Manage().Window.Size;

    /// <summary>
    /// Rotates the device and waits for the tree to settle.
    /// </summary>
    /// <remarks>
    /// The pause is not optional. MAUI rebuilds the shell asynchronously on a configuration
    /// change, and querying immediately after the rotation returns the pre-rotation layout —
    /// which then fails a placement assertion for a reason that has nothing to do with the app.
    /// </remarks>
    public void Rotate(ScreenOrientation orientation)
    {
        _driver.Orientation = orientation;
        Thread.Sleep(Timeouts.OrientationSettle);
    }

    /// <summary>
    /// Returns the app to a known state: portrait, on Home.
    /// </summary>
    /// <remarks>
    /// The Appium session is shared across the whole collection and tests mutate both orientation
    /// and the current page, so any test with a starting-state assumption must establish it
    /// rather than inherit whatever ran before it.
    /// </remarks>
    public void ResetToHome()
    {
        Rotate(ScreenOrientation.Portrait);
        Navigation.GoTo(NavDestination.Home);
        Home.WaitUntilVisible();
    }

    /// <summary>
    /// Restarts the app process, preserving persisted state.
    /// </summary>
    /// <returns>
    /// <c>false</c> when the environment does not support lifecycle commands, so a caller can
    /// skip rather than fail — the inability to restart is not a defect in the app.
    /// </returns>
    public bool TryRestart()
    {
        try
        {
            _driver.TerminateApp(PackageName);
            _driver.ActivateApp(PackageName);
        }
        catch
        {
            return false;
        }

        // ActivateApp returns as soon as the launch intent is dispatched, not when the app is
        // actually drawing. Returning here without waiting hands the caller a device still showing
        // the launcher, and its next assertion fails against that instead of against the app.
        var deadline = DateTime.UtcNow + Timeouts.AppStart;
        while (DateTime.UtcNow < deadline)
        {
            if (IsInForeground)
                return true;

            Thread.Sleep(250);
        }

        return false;
    }

    /// <summary>The package currently in the foreground, or empty if it cannot be read.</summary>
    public string ForegroundPackage
    {
        get
        {
            try
            {
                return _driver.CurrentPackage ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }
    }

    /// <summary>True when our app — not the launcher, not a system dialog — is in front.</summary>
    public bool IsInForeground =>
        string.Equals(ForegroundPackage, PackageName, StringComparison.Ordinal);

    /// <summary>
    /// The app is in front and has drawn something.
    /// </summary>
    /// <remarks>
    /// The package check is the load-bearing half. An earlier version asked only "is any element
    /// with text on screen", which the Android launcher satisfies trivially — so the startup smoke
    /// test reported success while the app was not running at all. That is the same vacuous-green
    /// shape this suite was rebuilt to eliminate, reintroduced one layer down.
    /// </remarks>
    public bool HasRenderedContent()
    {
        if (!IsInForeground)
            return false;

        try
        {
            return _driver
                .FindElements(By.XPath("//*[@text and string-length(@text) > 0]"))
                .Any(e =>
                {
                    try
                    {
                        return e.Displayed;
                    }
                    catch (StaleElementReferenceException)
                    {
                        return false;
                    }
                });
        }
        catch (StaleElementReferenceException)
        {
            return false;
        }
    }

    /// <summary>
    /// Guarantees the app is running and in front, relaunching it if it is not.
    /// </summary>
    /// <remarks>
    /// <para>
    /// One Appium session is shared by the whole collection, so whatever the previous test left
    /// behind is what the next one starts from. When a test terminates the app — two of them
    /// restart it deliberately — or the system kills it, every subsequent test fails on a device
    /// showing the launcher, reporting a confusing "page did not become visible" instead of the
    /// truth. Making each test establish the app itself is what turns those cascades back into a
    /// single honest failure.
    /// </para>
    /// <para>
    /// Deliberately does <b>not</b> swallow an app that will not start: if the relaunch does not
    /// bring the app to the foreground, this throws naming the package that is actually in front,
    /// so "the app is not running" is what the report says.
    /// </para>
    /// </remarks>
    public void EnsureInForeground()
    {
        if (IsInForeground)
            return;

        var before = ForegroundPackage;

        try
        {
            _driver.ActivateApp(PackageName);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"The app was not running (foreground package '{before}') and could not be " +
                $"relaunched: {ex.GetType().Name}: {ex.Message}", ex);
        }

        var deadline = DateTime.UtcNow + Timeouts.AppStart;
        while (DateTime.UtcNow < deadline)
        {
            if (IsInForeground)
                return;

            Thread.Sleep(250);
        }

        throw new InvalidOperationException(
            $"The app was not running (foreground package '{before}') and did not return to the " +
            $"foreground within {Timeouts.AppStart.TotalSeconds:0}s of being relaunched — the " +
            $"foreground package is now '{ForegroundPackage}'. It most likely crashed; check the " +
            "logcat crash buffer in the emulator diagnostics.");
    }
}
