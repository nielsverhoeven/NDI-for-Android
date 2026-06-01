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

        // The Sources page is the shell home — look for a TextView with text "Sources"
        // or the accessibility id on the tab/title.
        var sourcesElement = wait.Until(d =>
        {
            try
            {
                return d.FindElement(By.XPath(
                    "//*[@text='Sources' or @content-desc='Sources']"));
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
                    "//*[@text='Settings' or @content-desc='Settings']"));
            }
            catch (NoSuchElementException)
            {
                return null;
            }
        });

        Assert.NotNull(settingsTab);
        settingsTab!.Click();

        // Wait for Settings page to appear (look for a heading or any Settings label)
        var settingsPage = wait.Until(d =>
        {
            try
            {
                return d.FindElement(By.XPath(
                    "//*[@text='Settings' or @content-desc='Settings Page']"));
            }
            catch (NoSuchElementException)
            {
                return null;
            }
        });

        Assert.NotNull(settingsPage);

        // Navigate back to Sources
        driver.Navigate().Back();

        var sourcesElement = wait.Until(d =>
        {
            try
            {
                return d.FindElement(By.XPath(
                    "//*[@text='Sources' or @content-desc='Sources']"));
            }
            catch (NoSuchElementException)
            {
                return null;
            }
        });

        Assert.NotNull(sourcesElement);
    }
}

[CollectionDefinition("AppiumSession")]
public sealed class AppiumSessionCollection : ICollectionFixture<AppiumDriverFixture> { }
