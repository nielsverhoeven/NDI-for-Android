using System.Text;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium.Appium.Enums;
using Xunit;

namespace NdiForAndroid.UITests;

[Collection("AppiumSession")]
public sealed class AppLaunchTests
{
    private readonly AppiumDriverFixture _fixture;

    public AppLaunchTests(AppiumDriverFixture fixture)
    {
        _fixture = fixture;
    }

    // ── Page anchors ─────────────────────────────────────────────────────────
    //
    // Text each page genuinely renders, traceable to the XAML. Keep these in sync with the
    // views; an anchor that matches nothing turns into a timeout that looks like a navigation
    // failure, which is exactly how the previous "Sources" anchors misled.
    //
    // A page's Shell Title is a legitimate anchor here — it is a true indicator of the current
    // page. It is only unusable in WaitForNavElement, where it collides with the nav item.

    /// <summary>HomePage: "Discovery Status" / "Quick Actions" section headers.</summary>
    private const string HomeAnchor =
        "//*[@text='Discovery Status' or @text='Quick Actions' or @text='Viewer Status']";

    /// <summary>OutputPage (Stream tab): "Stream Name" label, "Start Output" button.</summary>
    private const string StreamAnchor =
        "//*[@text='Stream Name' or @text='Start Output' or @text='Stop Output' or @text='Capture:']";

    /// <summary>
    /// SourceListPage (View tab): Title="NDI Sources", plus the EmptyView text, which is what
    /// actually shows in CI — the emulator has no NDI sources on its network, so the list is
    /// empty and the row templates ("Watch"/"Output") never render.
    /// </summary>
    private const string ViewAnchor =
        "//*[@text='NDI Sources' or contains(@text,'No NDI sources found')]";

    /// <summary>SettingsPage: sidebar section buttons, which Android may render upper-cased.</summary>
    private const string SettingsAnchor =
        "//*[@text='General' or @text='GENERAL' or @text='Appearance' or @text='APPEARANCE']";

    [SkippableFact]
    public void AppLaunches_ShowsSourceListPage()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason ?? string.Empty);

        var driver = _fixture.Driver!;

        // Home is the shell entry point after the adaptive navigation parity update.
        //
        // Asserts on HomePage's own content rather than on the label "Home". The previous
        // locator matched the Shell top app bar title, so it passed as long as any title was
        // rendered — it would have passed with the page body completely broken.
        ResetToHome(driver);
        AssertPageVisible(driver, HomeAnchor, 20);
    }

    [SkippableFact]
    public void Navigation_ToSettings_AndBack()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason ?? string.Empty);

        var driver = _fixture.Driver!;

        // Starts from a known page: "Settings" would otherwise match the top app bar title
        // when the suite happens to leave the app already on Settings, and the tap would be a
        // no-op on a label rather than a navigation.
        ResetToHome(driver);

        ClickNav(driver, "Settings");
        AssertPageVisible(driver, SettingsAnchor, 20);

        // Navigate back to Home by tapping the Home nav item (Back press closes the Shell app)
        ClickNav(driver, "Home");
        AssertPageVisible(driver, HomeAnchor, 20);
    }

    [SkippableFact]
    public void Navigation_SourcesToViewer_WhenWatchButtonPresent()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason ?? string.Empty);

        var driver = _fixture.Driver!;

        // Ensure we are on the Home tab before trying to locate a source-row action.
        ResetToHome(driver);

        // Only run this check when a source row exists. In CI this skips: the emulator has no
        // NDI sources on its network, so the list renders its EmptyView and no row templates.
        // That is a genuine unmet precondition rather than a masked failure — the suite-level
        // guard in run-emulator-tests.sh still requires that other tests actually passed.
        var watchButtons = driver.FindElements(By.XPath("//*[@text='Watch']"));
        Skip.If(watchButtons.Count == 0, "No discovered NDI source rows available; skipping Home->Viewer smoke path.");

        watchButtons[0].Click();

        var viewerHeader = FindElement(driver, "//*[@content-desc='Viewer' or @text='Viewer']", 20);

        Assert.NotNull(viewerHeader);
    }

    [SkippableFact]
    public void AdaptiveNavigation_Portrait_ShowsBottomPlacement()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason ?? string.Empty);

        var driver = _fixture.Driver!;
        SetOrientation(driver, ScreenOrientation.Portrait);

        var home = WaitForNavElement(driver, "Home", 12);
        Assert.NotNull(home);

        var window = driver.Manage().Window.Size;
        Assert.True(home!.Location.Y > (int)(window.Height * 0.70),
            $"Expected Home nav element near bottom in portrait. y={home.Location.Y}, height={window.Height}");
    }

    [SkippableFact]
    public void AdaptiveNavigation_Landscape_ShowsLeftRailPlacement()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason ?? string.Empty);

        var driver = _fixture.Driver!;
        SetOrientation(driver, ScreenOrientation.Landscape);

        var home = WaitForNavElement(driver, "Home", 12);
        Assert.NotNull(home);

        var window = driver.Manage().Window.Size;
        Assert.True(home!.Location.X < (int)(window.Width * 0.20),
            $"Expected Home nav element near left edge in landscape. x={home.Location.X}, width={window.Width}");
        Assert.True(home.Location.Y < (int)(window.Height * 0.60),
            $"Expected Home nav element in left rail, not bottom bar. y={home.Location.Y}, height={window.Height}");
    }

    [SkippableFact]
    public void AdaptiveNavigation_AllFourPrimaryDestinations_AreReachable()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason ?? string.Empty);

        var driver = _fixture.Driver!;
        SetOrientation(driver, ScreenOrientation.Portrait);

        // Each destination is confirmed by text the page actually renders. The previous
        // anchors were written against an older UI: HomePage has no literal "Sources" — it
        // shows "Discovery Status" and "Sources found: {n}" — so that assertion could never
        // pass regardless of whether navigation worked.
        ClickNav(driver, "Home");
        AssertPageVisible(driver, HomeAnchor, 20);

        ClickNav(driver, "Stream");
        AssertPageVisible(driver, StreamAnchor, 20);

        ClickNav(driver, "View");
        AssertPageVisible(driver, ViewAnchor, 20);

        ClickNav(driver, "Settings");
        AssertPageVisible(driver, SettingsAnchor, 20);
    }

    [SkippableFact]
    public void Settings_RequiredSections_AreVisible()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason ?? string.Empty);

        var driver = _fixture.Driver!;

        ClickNav(driver, "Settings");

        // Section nav buttons use exact XAML text; also guard against Android all-caps button rendering
        AssertPageVisible(driver, "//*[@text='General' or @text='GENERAL']", 15);
        AssertPageVisible(driver, "//*[@text='Appearance' or @text='APPEARANCE']", 15);
        AssertPageVisible(driver, "//*[@text='Discovery' or @text='DISCOVERY']", 15);
        AssertPageVisible(driver, "//*[@text='Developer tools' or @text='DEVELOPER TOOLS']", 15);
        AssertPageVisible(driver, "//*[@text='About' or @text='ABOUT']", 15);
    }

    [SkippableFact]
    public void Settings_Save_PersistsDiscoveryHostAcrossRestart_WhenEnvironmentSupportsLifecycleCommands()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason ?? string.Empty);

        var driver = _fixture.Driver!;

        // Establish a known starting point: the session is shared, and this test previously
        // failed at the Discovery button because it inherited whatever page and orientation
        // the preceding test left behind.
        SetOrientation(driver, ScreenOrientation.Portrait);
        ClickNav(driver, "Settings");
        AssertPageVisible(driver, SettingsAnchor, 20);

        // The settings page has a left sidebar with section buttons. Discovery host/port
        // inputs live inside the Discovery section panel which is hidden by default.
        // Click the Discovery sidebar button first to reveal the EditText fields.
        // 20s, not 10s: the repo's floor for a cold emulator is 30s, and 10s was below what
        // the Settings page needs to render — the sibling test asserting the same text with a
        // 15s budget passes.
        var discoveryNavButton = FindElement(driver, "//*[@text='Discovery' or @text='DISCOVERY']", 20);
        Assert.NotNull(discoveryNavButton);
        discoveryNavButton!.Click();

        var hostEntry = FindElement(driver, "(//android.widget.EditText)[1]", 15);
        Assert.NotNull(hostEntry);

        hostEntry!.Clear();
        hostEntry.SendKeys("persist.test.local");

        var saveButton = FindElement(driver, "//*[@text='Apply' or @content-desc='Apply']", 10);
        Assert.NotNull(saveButton);
        saveButton!.Click();

        AssertPageVisible(driver, "//*[@text='Settings applied.']", 10);

        var packageName = "com.ndi.android";
        try
        {
            driver.TerminateApp(packageName);
            driver.ActivateApp(packageName);
        }
        catch
        {
            Skip.If(true, "App lifecycle commands are not supported in this execution environment.");
        }

        ClickNav(driver, "Settings");
        AssertPageVisible(driver, SettingsAnchor, 20);
        // Navigate to Discovery section again after restart to reveal EditText fields
        var discoveryNavButtonAfterRestart = FindElement(driver, "//*[@text='Discovery' or @text='DISCOVERY']", 20);
        Assert.NotNull(discoveryNavButtonAfterRestart);
        discoveryNavButtonAfterRestart!.Click();

        var hostEntryAfterRestart = FindElement(driver, "(//android.widget.EditText)[1]", 15);
        Assert.NotNull(hostEntryAfterRestart);

        var persistedValue = hostEntryAfterRestart!.GetAttribute("text") ?? string.Empty;
        Assert.Equal("persist.test.local", persistedValue);
    }

    private static void SetOrientation(AndroidDriver driver, ScreenOrientation orientation)
    {
        driver.Orientation = orientation;
        Thread.Sleep(1200);
    }

    /// <summary>
    /// Finds the navigation item carrying <paramref name="label"/> — the bottom tab in
    /// portrait, or the left rail item in landscape.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The obvious XPath is ambiguous. MAUI Shell renders the current page's <c>Title</c> in the
    /// top app bar, so while Home is showing, the text "Home" also appears there — earlier in
    /// document order than the tab. <c>FindElement</c> returns the first match, so a naive
    /// locator measures and clicks the title instead of the navigation item. That is why
    /// <see cref="AdaptiveNavigation_Portrait_ShowsBottomPlacement"/> reported the Home element
    /// at y=135 on a 2560px screen.
    /// </para>
    /// <para>
    /// The tie is broken on <b>interactivity</b>: navigation items are clickable, the title is
    /// not. It must not be broken on position — these locators feed the placement assertions,
    /// and picking "the candidate nearest the bottom" would make "assert it is near the bottom"
    /// prove nothing. That is the same vacuous-assertion trap this suite already fell into.
    /// </para>
    /// </remarks>
    private static string LabelMatch(string label) =>
        $"@content-desc='{label}' or contains(@content-desc,'{label}') or @text='{label}'";

    private static IWebElement? WaitForNavElement(AndroidDriver driver, string label, int timeoutSeconds)
    {
        var match = LabelMatch(label);

        // Interactivity is resolved inside a single server-side XPath. Walking ancestors from
        // C# instead costs one round trip per node, and UiAutomator2 round trips are slow
        // enough that several candidates × several levels exhausted the whole wait before it
        // could poll twice — every call timed out without ever evaluating a real candidate.
        var navXpath = $"//*[({match}) and ancestor-or-self::*[@clickable='true']]";

        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            return wait.Until(d =>
            {
                try
                {
                    foreach (var candidate in d.FindElements(By.XPath(navXpath)))
                    {
                        if (candidate.Displayed)
                            return candidate;
                    }
                }
                catch (StaleElementReferenceException)
                {
                    // The tree changed mid-scan (common during a navigation transition) — retry.
                }

                return null;
            });
        }
        catch (WebDriverTimeoutException)
        {
            // A bare "timed out" says nothing about why. Dump what the label actually matched
            // and how each candidate is classified, so one run identifies the tree shape
            // rather than costing another round of guesses.
            throw new WebDriverTimeoutException(DescribeNavCandidates(driver, label, match));
        }
    }

    /// <summary>
    /// Builds a diagnostic listing every element carrying the label and why it was or was not
    /// treated as a navigation item.
    /// </summary>
    private static string DescribeNavCandidates(AndroidDriver driver, string label, string match)
    {
        var report = new StringBuilder()
            .AppendLine($"No interactive navigation item found for '{label}'.")
            .AppendLine("Elements matching the label:");

        try
        {
            var candidates = driver.FindElements(By.XPath($"//*[{match}]"));

            if (candidates.Count == 0)
            {
                report.AppendLine("  (none — the label is not present in the tree at all)");
            }

            foreach (var candidate in candidates)
            {
                report.AppendLine(
                    $"  class={candidate.GetAttribute("class")} " +
                    $"text='{candidate.Text}' " +
                    $"desc='{candidate.GetAttribute("content-desc")}' " +
                    $"clickable={candidate.GetAttribute("clickable")} " +
                    $"displayed={candidate.Displayed} " +
                    $"at={candidate.Location.X},{candidate.Location.Y} " +
                    $"size={candidate.Size.Width}x{candidate.Size.Height}");
            }

            var clickableAncestors = driver.FindElements(
                By.XPath($"//*[({match}) and ancestor-or-self::*[@clickable='true']]")).Count;
            report.AppendLine($"Candidates with a clickable ancestor-or-self: {clickableAncestors}");
        }
        catch (Exception ex)
        {
            report.AppendLine($"  (could not enumerate candidates: {ex.GetType().Name}: {ex.Message})");
        }

        return report.ToString();
    }

    /// <summary>
    /// Returns the suite to a known state: portrait, on Home. The session is shared across the
    /// whole collection and tests mutate orientation and the current page, so anything that
    /// depends on a starting state must establish it rather than inherit whatever ran last.
    /// </summary>
    private static void ResetToHome(AndroidDriver driver)
    {
        SetOrientation(driver, ScreenOrientation.Portrait);
        ClickNav(driver, "Home");
    }

    private static void ClickNav(AndroidDriver driver, string label)
    {
        var element = WaitForNavElement(driver, label, 30);
        Assert.NotNull(element);
        element!.Click();
    }

    private static void AssertPageVisible(AndroidDriver driver, string xpath, int timeoutSeconds = 12)
    {
        var found = FindElement(driver, xpath, timeoutSeconds);

        Assert.NotNull(found);
    }

    private static IWebElement? FindElement(AndroidDriver driver, string xpath, int timeoutSeconds)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
        return wait.Until(d =>
        {
            try
            {
                return d.FindElement(By.XPath(xpath));
            }
            catch (NoSuchElementException)
            {
                return null;
            }
        });
    }
}

[CollectionDefinition("AppiumSession")]
public sealed class AppiumSessionCollection : ICollectionFixture<AppiumDriverFixture> { }
