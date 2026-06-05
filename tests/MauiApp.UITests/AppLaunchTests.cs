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

        // The Sources page is the shell home — look for the Sources tab accessibility id
        var sourcesElement = wait.Until(d =>
        {
            try
            {
                return d.FindElement(By.XPath(
                    "//*[@content-desc='Sources' or @text='NDI Sources']"));
            }
            catch (NoSuchElementException)
            {
                return null;
            }
        });

        Assert.NotNull(sourcesElement);
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
                    "//*[@content-desc='Settings']"));
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
                    "//*[@text='Discovery Server' or @text='Save' or @text='Settings saved.']"));
            }
            catch (NoSuchElementException)
            {
                return null;
            }
        });

        Assert.NotNull(settingsPage);

        // Navigate back to Sources by tapping the Sources tab (Back press closes the Shell app)
        var sourcesTab = wait.Until(d =>
        {
            try
            {
                return d.FindElement(By.XPath(
                    "//*[@content-desc='Sources']"));
            }
            catch (NoSuchElementException)
            {
                return null;
            }
        });

        Assert.NotNull(sourcesTab);
        sourcesTab!.Click();

        var sourcesElement = wait.Until(d =>
        {
            try
            {
                return d.FindElement(By.XPath(
                    "//*[@content-desc='Sources' or @text='NDI Sources']"));
            }
            catch (NoSuchElementException)
            {
                return null;
            }
        });

        Assert.NotNull(sourcesElement);
    }

    [SkippableFact]
    public void Navigation_SourcesToViewer_WhenWatchButtonPresent()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason ?? string.Empty);

        var driver = _fixture.Driver!;
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(20));

        // Ensure we are on Sources tab before trying to locate a row action.
        var sourcesTab = wait.Until(d =>
        {
            try
            {
                return d.FindElement(By.XPath("//*[@content-desc='Sources']"));
            }
            catch (NoSuchElementException)
            {
                return null;
            }
        });

        Assert.NotNull(sourcesTab);
        sourcesTab!.Click();

        // Only run this check when a source row exists.
        var watchButtons = driver.FindElements(By.XPath("//*[@text='Watch']"));
        Skip.If(watchButtons.Count == 0, "No discovered NDI source rows available; skipping Sources->Viewer smoke path.");

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
        AssertPageVisible(driver, "//*[@text='Viewer' or contains(@content-desc,'Viewer')]", 15);

        ClickNav(driver, "Settings");
        AssertPageVisible(driver, "//*[@text='Discovery Server' or @text='Save' or @text='Settings saved.']", 15);
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
        var element = WaitForNavElement(driver, label, 12);
        Assert.NotNull(element);
        element!.Click();
    }

    private static void AssertPageVisible(AndroidDriver driver, string xpath, int timeoutSeconds = 12)
    {
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutSeconds));
        var found = wait.Until(d =>
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

        Assert.NotNull(found);
    }
}

[CollectionDefinition("AppiumSession")]
public sealed class AppiumSessionCollection : ICollectionFixture<AppiumDriverFixture> { }
