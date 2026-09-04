using NdiForAndroid.UITests.Infrastructure;
using Xunit;

namespace NdiForAndroid.UITests;

/// <summary>
/// The app launches and reaches a visible UI state after installation.
/// </summary>
/// <remarks>
/// Guards the startup-abort regression from #153 (libmonodroid Fast Deployment abort). If the APK
/// aborts on startup the Appium session cannot be created at all, so in CI — where
/// <c>E2E_REQUIRE_DEVICE=true</c> — the fixture throws during setup and the suite goes red rather
/// than reporting a pass it never earned.
/// </remarks>
[Collection("AppiumSession")]
public sealed class StartupSmokeTests : UiTestBase
{
    public StartupSmokeTests(AppiumDriverFixture fixture) : base(fixture) { }

    /// <summary>
    /// The app survives startup far enough for Appium to attach.
    /// </summary>
    /// <remarks>
    /// Reaching the test body <i>is</i> the assertion: <see cref="UiTestBase.Run"/> only gets a
    /// driver once the session was created, which cannot happen if the process aborted at the
    /// Fast Deployment check.
    /// </remarks>
    [SkippableFact]
    public void AppStartup_DoesNotAbort_SessionIsEstablished() => Run(app =>
    {
        Assert.NotNull(app);
    });

    /// <summary>
    /// The app renders something within the cold-start budget.
    /// </summary>
    /// <remarks>
    /// Catches a process that starts and then exits without drawing — a silent crash after the
    /// Appium session exists, which the session check above cannot see. The name used to promise
    /// 15 seconds while the code waited 30; the budget now comes from
    /// <see cref="Timeouts.AppStart"/> and the name no longer states a number it does not own.
    /// </remarks>
    [SkippableFact]
    public void AppStartup_RendersUiWithinTheColdStartBudget() => Run(app =>
    {
        var deadline = DateTime.UtcNow + Timeouts.AppStart;

        while (DateTime.UtcNow < deadline)
        {
            if (app.HasRenderedContent())
                return;

            Thread.Sleep(250);
        }

        Assert.Fail(
            $"No visible text rendered within {Timeouts.AppStart.TotalSeconds:0}s of launch. " +
            "See the captured screenshot and view hierarchy for what was on screen.");
    });
}
