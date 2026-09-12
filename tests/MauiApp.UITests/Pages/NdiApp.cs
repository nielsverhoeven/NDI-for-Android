using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
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

        // A configuration change is one of the few things that can take the whole process down —
        // it tears down and rebuilds the Shell, and this app swaps its entire navigation
        // implementation at that point (bottom tab bar to left rail). If that kills the app, the
        // next call reports "page did not become visible" against a device showing the launcher,
        // which points investigation at the page rather than at the rotation that caused it.
        if (!IsInForeground)
            throw new InvalidOperationException(
                $"The app stopped running while rotating to {orientation} — the foreground " +
                $"package is now '{ForegroundPackage}'. The rotation itself brought the app down; " +
                "see the logcat crash buffer in the emulator diagnostics.");
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
    /// <remarks>
    /// <para>
    /// Asks the view tree, not just <c>CurrentPackage</c>. After an <c>am force-stop</c> —
    /// which is exactly what <see cref="TryRestart"/> issues — ActivityManager logs
    /// <c>Force removing ActivityRecord … app died, no saved state</c>, yet <c>CurrentPackage</c>
    /// kept reporting <c>com.ndi.android</c> while the device was plainly showing the launcher.
    /// A package-only check therefore declared the app healthy and skipped the relaunch, and every
    /// later test failed against the launcher with a message blaming a page.
    /// </para>
    /// <para>
    /// The presence of a view owned by our package cannot be wrong in that way: Android namespaces
    /// <c>resource-id</c> by package, so a node under <c>com.ndi.android:id/</c> exists only if our
    /// process is actually rendering.
    /// </para>
    /// </remarks>
    public bool IsInForeground
    {
        get
        {
            if (!string.Equals(ForegroundPackage, PackageName, StringComparison.Ordinal))
                return false;

            try
            {
                return _driver
                    .FindElements(By.XPath($"//*[starts-with(@resource-id, '{PackageName}:')]"))
                    .Count > 0;
            }
            catch (Exception)
            {
                // A tree that cannot be read is not evidence the app is up.
                return false;
            }
        }
    }

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
            // ActivateApp resolves and starts the launcher intent, which is what brings the app
            // back after a force-stop. Calling it when the process is already gone is safe.
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

    // ── Device-level actions for deep links, permissions and interruptions ──────────────────

    /// <summary>Force-stops the app without relaunching it.</summary>
    public void Terminate() => _driver.TerminateApp(PackageName);

    /// <summary>
    /// Fires an <c>ndi://</c> deep link at the app via Appium's <c>mobile: deepLink</c> extension.
    /// Cold-starts the app if it is not running, or delivers <c>OnNewIntent</c> to an
    /// already-running process.
    /// </summary>
    public void DeepLink(string uri) => _driver.ExecuteScript("mobile: deepLink",
        new Dictionary<string, object> { ["url"] = uri, ["package"] = PackageName });

    /// <summary>Sends the app to the background indefinitely — the caller controls when it returns.</summary>
    public void SendToBackground() => _driver.ExecuteScript("mobile: backgroundApp",
        new Dictionary<string, object> { ["seconds"] = -1 });

    /// <summary>
    /// Runs an adb shell command through Appium's <c>mobile: shell</c> extension and returns its
    /// output. Requires the Appium server to be started with
    /// <c>--allow-insecure=uiautomator2:adb_shell</c>.
    /// </summary>
    public string ShellCommand(string command, params string[] args)
    {
        object? result;
        try
        {
            result = _driver.ExecuteScript("mobile: shell", new Dictionary<string, object>
            {
                ["command"] = command,
                ["args"] = args,
            });
        }
        catch (WebDriverException ex)
        {
            throw new InvalidOperationException(
                $"`mobile: shell` ({command} {string.Join(' ', args)}) failed. The Appium server " +
                "must be started with --allow-insecure=uiautomator2:adb_shell. " +
                $"Underlying error: {ex.Message}", ex);
        }

        // Some UiAutomator2 versions return {stdout, stderr} instead of a bare string.
        return result switch
        {
            null => string.Empty,
            string text => text,
            IDictionary<string, object> map => map.TryGetValue("stdout", out var stdout)
                ? stdout?.ToString() ?? string.Empty
                : string.Empty,
            _ => result.ToString() ?? string.Empty,
        };
    }

    /// <summary>
    /// Simulates or resolves a GSM call via the emulator console, through Appium's
    /// <c>mobile: gsmCall</c> extension. <paramref name="action"/> is one of "call", "accept",
    /// "cancel", "hold". Emulator-only.
    /// </summary>
    public void GsmCall(string phoneNumber, string action) => _driver.ExecuteScript("mobile: gsmCall",
        new Dictionary<string, object> { ["phoneNumber"] = phoneNumber, ["action"] = action });

    /// <summary>
    /// True if a system Toast whose text contains <paramref name="expectedSubstring"/> is
    /// currently showing. <c>MainActivity.ShowToast</c> renders a plain
    /// <c>Android.Widget.Toast</c>, not a MAUI element, so it carries no automation id —
    /// UiAutomator2 exposes a shown Toast as an accessibility node with
    /// <c>class="android.widget.Toast"</c>, which is what this matches. Matching on the expected
    /// text (not just the class) avoids a false positive against a stale Toast left over from an
    /// earlier test.
    /// </summary>
    public bool HasVisibleToast(string expectedSubstring, TimeSpan? timeout = null)
    {
        var wait = new WebDriverWait(_driver, timeout ?? TimeSpan.FromSeconds(4));
        try
        {
            return wait.Until(_ => _driver
                .FindElements(By.XPath(
                    $"//*[@class='android.widget.Toast' and contains(@text, '{expectedSubstring}')]"))
                .Count > 0);
        }
        catch (WebDriverTimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// Taps the system runtime-permission dialog's Allow or Deny button. The dialog belongs to
    /// <c>com.android.permissioncontroller</c>, not our app, so this is a fully-qualified resource
    /// id rather than a <c>TestIds</c> automation id. The deny id varies by how many times the
    /// permission has already been asked — this AVD image renders
    /// <c>permission_deny_and_dont_ask_again_button</c> once a prior ask exists; the plain
    /// <c>permission_deny_button</c> is tried as a fallback for an image that still has it.
    /// </summary>
    public void RespondToPermissionDialog(bool allow, TimeSpan? timeout = null)
    {
        const string permissionControllerPackage = "com.android.permissioncontroller";
        var primaryId = allow
            ? $"{permissionControllerPackage}:id/permission_allow_button"
            : $"{permissionControllerPackage}:id/permission_deny_and_dont_ask_again_button";
        var fallbackId = $"{permissionControllerPackage}:id/permission_deny_button";

        var wait = new WebDriverWait(_driver, timeout ?? Timeouts.Element);
        var buttonId = primaryId;
        IWebElement? button;
        try
        {
            button = wait.Until(_ =>
            {
                var match = _driver.FindElements(By.Id(primaryId)).FirstOrDefault(SafeDisplayed);
                if (match is not null)
                {
                    buttonId = primaryId;
                    return match;
                }

                if (allow)
                    return null;

                match = _driver.FindElements(By.Id(fallbackId)).FirstOrDefault(SafeDisplayed);
                if (match is not null)
                    buttonId = fallbackId;

                return match;
            });
        }
        catch (WebDriverTimeoutException)
        {
            throw new WebDriverTimeoutException(
                $"The system permission dialog's '{(allow ? "Allow" : "Don't allow")}' button " +
                $"('{primaryId}'{(allow ? "" : $", or fallback '{fallbackId}'")}) never appeared " +
                $"within {(timeout ?? Timeouts.Element).TotalSeconds:0}s. " +
                $"Current foreground package: '{ForegroundPackage}'.");
        }

        // A single Click() can silently miss a dialog still settling — verify the button actually
        // leaves the tree afterwards, alternating a direct click and a pointer tap at its centre.
        const int maxAttempts = 4;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                if (attempt % 2 == 1)
                    button!.Click();
                else
                    TapAtCentre(button!);
            }
            catch (StaleElementReferenceException)
            {
                // The dialog may have already dismissed between the find and the tap.
            }

            var deadline = DateTime.UtcNow + Timeouts.StateChange;
            IWebElement? stillThere;
            do
            {
                stillThere = _driver.FindElements(By.Id(buttonId)).FirstOrDefault(SafeDisplayed);
                if (stillThere is null)
                    return;

                Thread.Sleep(250);
            } while (DateTime.UtcNow < deadline);

            button = stillThere;
        }

        throw new InvalidOperationException(
            $"'{buttonId}' did not disappear after {maxAttempts} tap attempts (alternating a " +
            "direct click and a pointer tap at its centre) — the system permission dialog is not " +
            "responding to synthetic input.");
    }

    private void TapAtCentre(IWebElement element)
    {
        var location = element.Location;
        var size = element.Size;
        var x = location.X + size.Width / 2;
        var y = location.Y + size.Height / 2;

        var touch = new PointerInputDevice(PointerKind.Touch, "finger");
        var sequence = new ActionSequence(touch, 0);

        sequence.AddAction(touch.CreatePointerMove(CoordinateOrigin.Viewport, x, y, TimeSpan.Zero));
        sequence.AddAction(touch.CreatePointerDown(MouseButton.Touch));
        sequence.AddAction(touch.CreatePointerUp(MouseButton.Touch));

        _driver.PerformActions([sequence]);
    }

    /// <summary>
    /// Restarts the app and returns once either our own UI or the system permission dialog is
    /// showing. <see cref="TryRestart"/> cannot be reused here: it waits for
    /// <see cref="IsInForeground"/>, which requires our own package to be foreground — but while
    /// the permission dialog is up, the foreground package is
    /// <c>com.android.permissioncontroller</c>, so a restart expected to land on that dialog would
    /// spin for the full <see cref="Timeouts.AppStart"/> budget and report failure even though the
    /// app started correctly.
    /// </summary>
    public bool TryRestartExpectingPermissionPrompt()
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

        const string permissionControllerPackage = "com.android.permissioncontroller";
        var deadline = DateTime.UtcNow + Timeouts.AppStart;
        while (DateTime.UtcNow < deadline)
        {
            if (IsInForeground ||
                string.Equals(ForegroundPackage, permissionControllerPackage, StringComparison.Ordinal))
                return true;

            Thread.Sleep(250);
        }

        return false;
    }

    private static bool SafeDisplayed(IWebElement element)
    {
        try { return element.Displayed; } catch (StaleElementReferenceException) { return false; }
    }
}
