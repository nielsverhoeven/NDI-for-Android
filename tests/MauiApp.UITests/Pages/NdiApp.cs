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
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Any element on screen carrying non-empty text — proof the UI rendered at all.</summary>
    public bool HasRenderedContent()
    {
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
}
