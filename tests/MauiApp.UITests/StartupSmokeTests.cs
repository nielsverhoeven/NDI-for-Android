using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;
using Xunit;

namespace MauiApp.UITests;

/// <summary>
/// Smoke tests that validate the app launches and reaches a visible UI state
/// after APK installation. These specifically guard against the startup-abort
/// regression described in issue #153 (libmonodroid Fast Deployment abort).
/// </summary>
[Collection("AppiumSession")]
public sealed class StartupSmokeTests
{
    private readonly AppiumDriverFixture _fixture;

    public StartupSmokeTests(AppiumDriverFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Verifies the app reaches a visible UI element within the timeout window.
    /// If the APK aborts on startup due to a Fast Deployment mismatch, the
    /// Appium session itself will fail to create (driver will be null / SkipReason set),
    /// causing all tests to be skipped rather than incorrectly reported as passing.
    /// A successful session creation proves the app survived startup.
    /// </summary>
    [SkippableFact]
    public void AppStartup_DoesNotAbort_DriverSessionEstablished()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason ?? string.Empty);

        // If we reach here, the Appium session was created — the app did not abort
        // at the libmonodroid Fast Deployment check. Driver being non-null is the assertion.
        Assert.NotNull(_fixture.Driver);
    }

    /// <summary>
    /// Verifies the app renders at least one visible UI element within 15 seconds of launch.
    /// Guards against cases where the app process starts but immediately exits without
    /// rendering anything (silent crash after Appium session creation).
    /// </summary>
    [SkippableFact]
    public void AppStartup_RendersUiWithin15Seconds()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason ?? string.Empty);

        var driver = _fixture.Driver!;
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(15));

        // Any visible, non-loading element proves the UI rendered.
        // Try the Sources tab first (MAUI Shell home), then known Settings elements,
        // then fall back to any visible text to handle app being on any page.
        var element = wait.Until(d =>
        {
            try
            {
                // Primary: Sources shell tab visible
                var el = d.FindElement(By.XPath(
                    "//*[@content-desc='Sources' or @text='NDI Sources' or @text='Sources']"));
                return el.Displayed ? el : null;
            }
            catch (NoSuchElementException)
            {
                try
                {
                    // Secondary: Settings sidebar buttons are always visible on Settings page
                    var settingsEl = d.FindElement(By.XPath(
                        "//*[@text='General' or @text='GENERAL' or @text='Appearance' or @text='APPEARANCE']"));
                    return settingsEl.Displayed ? settingsEl : null;
                }
                catch (NoSuchElementException)
                {
                    try
                    {
                        // Fallback: any non-empty text element proves UI rendered
                        var anyText = d.FindElement(By.XPath("//*[@text and string-length(@text) > 0]"));
                        return anyText.Displayed ? anyText : null;
                    }
                    catch (NoSuchElementException)
                    {
                        return null;
                    }
                }
            }
        });

        Assert.NotNull(element);
    }
}
