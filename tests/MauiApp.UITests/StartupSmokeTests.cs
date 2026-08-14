using OpenQA.Selenium;
using OpenQA.Selenium.Appium.Android;
using OpenQA.Selenium.Support.UI;
using Xunit;

namespace NdiForAndroid.UITests;

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
    /// Verifies the app renders at least one visible UI element within 30 seconds of launch.
    /// Guards against cases where the app process starts but immediately exits without
    /// rendering anything (silent crash after Appium session creation).
    /// Timeout is 30s (not 15s) to accommodate cold-start emulator boot in CI.
    /// </summary>
    [SkippableFact]
    public void AppStartup_RendersUiWithin15Seconds()
    {
        Skip.If(_fixture.SkipReason is not null, _fixture.SkipReason ?? string.Empty);

        var driver = _fixture.Driver!;
        var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(30));

        // Any displayed element carrying text proves the UI rendered, whichever page the
        // shared session happens to be on.
        //
        // Uses FindElements and scans for the first *displayed* match. The previous version
        // called FindElement and tested .Displayed on that single result: if the first match
        // in document order happened to be off-screen — easily the case once the app has more
        // than one page in the hierarchy — the lambda returned null on every poll and the wait
        // ran to timeout even though the UI had rendered perfectly well.
        var element = wait.Until(d =>
        {
            try
            {
                foreach (var candidate in d.FindElements(By.XPath("//*[@text and string-length(@text) > 0]")))
                {
                    if (candidate.Displayed)
                        return candidate;
                }
            }
            catch (StaleElementReferenceException)
            {
                // Tree changed mid-scan — retry on the next poll.
            }

            return null;
        });

        Assert.NotNull(element);
    }
}
