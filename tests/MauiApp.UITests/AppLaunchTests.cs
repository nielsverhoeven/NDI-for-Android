using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;
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
}

[CollectionDefinition("AppiumSession")]
public sealed class AppiumSessionCollection : ICollectionFixture<AppiumDriverFixture> { }
