using NdiForAndroid.UITests.Pages;

namespace NdiForAndroid.UITests;

/// <summary>
/// The permission-flow driver session: identical to <see cref="AppiumDriverFixture"/> except
/// <c>autoGrantPermissions</c> is off, so the real POST_NOTIFICATIONS system dialog appears
/// instead of being pre-answered.
/// </summary>
/// <remarks>
/// Must run in a collection separate from "AppiumSession" — see <c>PermissionSessionCollection</c>
/// in PermissionTests.cs. The assembly-level <c>DisableTestParallelization</c> in AssemblyInfo.cs
/// is what actually keeps the two collections' Appium sessions from overlapping; this class only
/// supplies the different capability.
/// </remarks>
public sealed class PermissionAppiumDriverFixture : AppiumDriverFixture
{
    protected override bool AutoGrantPermissions => false;
    protected override bool AutoLaunch => false;

    /// <summary>
    /// Launches the app ourselves — Appium's own launch-wait is disabled via
    /// <see cref="AutoLaunch"/> — and tolerates landing on either our own UI or the system
    /// permission dialog, the same way <see cref="NdiApp.TryRestartExpectingPermissionPrompt"/>
    /// does for a mid-session restart. The very first launch of a freshly-installed app
    /// immediately shows the POST_NOTIFICATIONS dialog; dismissing it here, once, before any test
    /// runs, gives every test a known starting point — each test still does its own
    /// revoke-then-restart cycle regardless of what this leaves behind. A broken baseline must
    /// fail the whole collection loudly rather than leak an undismissed dialog into the first test,
    /// so neither step here swallows a failure.
    /// </summary>
    protected override Task AfterDriverCreatedAsync()
    {
        if (Driver is null)
            return Task.CompletedTask;

        var app = new NdiApp(Driver);
        if (!app.TryRestartExpectingPermissionPrompt())
            throw new InvalidOperationException(
                "The permission-flow session's initial launch never reached our own UI or the " +
                "system permission dialog.");

        app.RespondToPermissionDialog(allow: false);

        return Task.CompletedTask;
    }
}
