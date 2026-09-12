using NdiForAndroid.UITests.Infrastructure;
using NdiForAndroid.UITests.Pages;
using Xunit;

namespace NdiForAndroid.UITests;

/// <summary>
/// The POST_NOTIFICATIONS runtime-permission flow, with the real system dialog visible. Runs in
/// its own Appium session (<c>autoGrantPermissions=false</c>) and its own xUnit collection so it
/// never overlaps the main "AppiumSession" collection's auto-granting session — see
/// AssemblyInfo.cs for the assembly-wide <c>DisableTestParallelization</c> that keeps the two
/// collections from running at the same time against the single emulator.
/// </summary>
[Collection("PermissionSession")]
public sealed class PermissionTests : DeviceInterruptionTestBase
{
    public PermissionTests(PermissionAppiumDriverFixture fixture) : base(fixture) { }

    /// <summary>
    /// Forces the permission back to "not yet decided" and restarts the process so
    /// <c>OnCreate</c> issues a fresh <c>Permissions.RequestAsync&lt;PostNotifications&gt;()</c>
    /// call. A bare <c>pm revoke</c> does not by itself make the app ask again — only a new
    /// process does. This also removes any leakage from the main collection's own auto-granting
    /// session, which may have run against the same installed app either before or after this
    /// collection.
    /// </summary>
    private static void ResetAndRestart(NdiApp app)
    {
        app.ShellCommand("pm", "revoke", NdiApp.PackageName, "android.permission.POST_NOTIFICATIONS");
        Assert.True(app.TryRestartExpectingPermissionPrompt(),
            "The app did not relaunch (or reach the permission prompt) after the permission was reset");
    }

    [SkippableFact]
    public void PostNotifications_Granted_LeavesTheAppUsable() => RunWithCrashGate(app =>
    {
        ResetAndRestart(app);

        app.RespondToPermissionDialog(allow: true, Timeouts.AppStart);

        app.Home.WaitUntilVisible(Timeouts.Navigation);
        Assert.True(app.Home.HasDiscoveryCard, "Home did not render after granting POST_NOTIFICATIONS");
    });

    [SkippableFact]
    public void PostNotifications_Denied_LeavesTheAppUsable() => RunWithCrashGate(app =>
    {
        ResetAndRestart(app);

        // Taps "Don't allow", not the "don't ask again" variant, so the permission stays askable
        // for whichever test (or collection) runs next.
        app.RespondToPermissionDialog(allow: false, Timeouts.AppStart);

        app.Home.WaitUntilVisible(Timeouts.Navigation);
        Assert.True(app.Home.HasDiscoveryCard, "Home did not render after denying POST_NOTIFICATIONS");
    });
}

[CollectionDefinition("PermissionSession")]
public sealed class PermissionSessionCollection : ICollectionFixture<PermissionAppiumDriverFixture> { }
