using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium.Appium.Enums;
using Xunit;

namespace MauiApp.UITests;

[Collection("AppiumSession")]
public sealed class AppLaunchTests
{
    private readonly AppiumDriverFixture _fixture;

    public AppLaunchTests(AppiumDriverFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public void AppLaunches_ShowsSourceListPage()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason ?? string.Empty);

        var driver = _fixture.Driver!;
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));

        // Home is the shell entry point after the adaptive navigation parity update.
        var homeElement = wait.Until(d =>
        {
            try
            {
                return d.FindElement(By.XPath(
                    "//*[@content-desc='Home' or @text='Home' or @text='NDI Sources']"));
            }
            catch (NoSuchElementException)
            {
                return null;
            }
        });

        Assert.NotNull(homeElement);
    }

    [SkippableFact]
    public void Navigation_ToSettings_AndBack()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason ?? string.Empty);

        var driver = _fixture.Driver!;
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));

        // Tap the Settings tab/button
        var settingsTab = wait.Until(d =>
        {
            try
            {
                return d.FindElement(By.XPath(
                    "//*[@content-desc='Settings' or @text='Settings']"));
            }
            catch (NoSuchElementException)
            {
                return null;
            }
        });

        Assert.NotNull(settingsTab);
        settingsTab!.Click();

        // Wait for Settings page content — look for a label unique to that page
        var settingsPageWait = new WebDriverWait(driver, TimeSpan.FromSeconds(20));
        var settingsPage = settingsPageWait.Until(d =>
        {
            try
            {
                return d.FindElement(By.XPath(
                    "//*[@text='General' or @text='Appearance' or @text='Discovery' or @text='Apply']"));
            }
            catch (NoSuchElementException)
            {
                return null;
            }
        });

        Assert.NotNull(settingsPage);

        // Navigate back to Home by tapping the Home tab/entry (Back press closes the Shell app)
        var homeTab = wait.Until(d =>
        {
            try
            {
                return d.FindElement(By.XPath(
                    "//*[@content-desc='Home' or @text='Home']"));
            }
            catch (NoSuchElementException)
            {
                return null;
            }
        });

        Assert.NotNull(homeTab);
        homeTab!.Click();

        var homeElement = wait.Until(d =>
        {
            try
            {
                return d.FindElement(By.XPath(
                    "//*[@content-desc='Home' or @text='Home' or @text='NDI Sources']"));
            }
            catch (NoSuchElementException)
            {
                return null;
            }
        });

        Assert.NotNull(homeElement);
    }

    [SkippableFact]
    public void Navigation_SourcesToViewer_WhenWatchButtonPresent()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason ?? string.Empty);

        var driver = _fixture.Driver!;
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(20));

        // Ensure we are on Home tab before trying to locate a source-row action.
        var homeTab = wait.Until(d =>
        {
            try
            {
                return d.FindElement(By.XPath("//*[@content-desc='Home' or @text='Home']"));
            }
            catch (NoSuchElementException)
            {
                return null;
            }
        });

        Assert.NotNull(homeTab);
        homeTab!.Click();

        // Only run this check when a source row exists.
        var watchButtons = driver.FindElements(By.XPath("//*[@text='Watch']"));
        Skip.If(watchButtons.Count == 0, "No discovered NDI source rows available; skipping Home->Viewer smoke path.");

        watchButtons[0].Click();

        var viewerHeader = wait.Until(d =>
        {
            try
            {
                return d.FindElement(By.XPath("//*[@content-desc='Viewer' or @text='Viewer']"));
            }
            catch (NoSuchElementException)
            {
                return null;
            }
        });

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

        ClickNav(driver, "Home");
        AssertPageVisible(driver, "//*[@content-desc='Sources' or @text='NDI Sources' or @text='Sources']");

        ClickNav(driver, "Stream");
        AssertPageVisible(driver, "//*[@text='Start Output' or @text='Stop Output' or contains(@content-desc,'Output')]", 15);

        ClickNav(driver, "View");
        // View tab now hosts SourceListPage (NDI source discovery), not ViewerPage directly
        AssertPageVisible(driver, "//*[@text='NDI Sources' or @text='Sources' or contains(@content-desc,'Sources') or contains(@content-desc,'mDNS') or contains(@content-desc,'Discovery')]", 15);

        ClickNav(driver, "Settings");
        AssertPageVisible(driver, "//*[@text='General' or @text='Appearance' or @text='Discovery' or @text='Apply']", 15);
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
        ClickNav(driver, "Settings");

        // The settings page has a left sidebar with section buttons. Discovery host/port
        // inputs live inside the Discovery section panel which is hidden by default.
        // Click the Discovery sidebar button first to reveal the EditText fields.
        var discoveryNavButton = FindElement(driver, "//*[@text='Discovery' or @text='DISCOVERY']", 10);
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
        // Navigate to Discovery section again after restart to reveal EditText fields
        var discoveryNavButtonAfterRestart = FindElement(driver, "//*[@text='Discovery' or @text='DISCOVERY']", 10);
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

    private static IWebElement? WaitForNavElement(AndroidDriver driver, string label, int timeoutSeconds)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
        var xpath = $"//*[@content-desc='{label}' or contains(@content-desc,'{label}') or @text='{label}']";

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

    private static void ClickNav(AndroidDriver driver, string label)
    {
        var element = WaitForNavElement(driver, label, 30);
        Assert.NotNull(element);
        element!.Click();
    }

    private static void AssertPageVisible(AndroidDriver driver, string xpath, int timeoutSeconds = 12)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
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
