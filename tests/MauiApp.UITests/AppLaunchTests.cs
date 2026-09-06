using OpenQA.Selenium;
using NdiForAndroid.Testing;
using NdiForAndroid.UITests.Infrastructure;
using NdiForAndroid.UITests.Pages;
using Xunit;

namespace NdiForAndroid.UITests;

/// <summary>
/// Navigation and page-content checks across the four primary destinations.
/// </summary>
/// <remarks>
/// Every locator lives in a page object; nothing here knows an XPath or an element id. Assertions
/// name the thing they are checking rather than asserting that <i>something</i> was found — the
/// previous <c>Assert.NotNull(element)</c> style is exactly how two tests passed against the
/// Shell page title instead of the content they claimed to verify.
/// </remarks>
[Collection("AppiumSession")]
public sealed class AppLaunchTests : UiTestBase
{
    public AppLaunchTests(AppiumDriverFixture fixture) : base(fixture) { }

    [SkippableFact]
    public void AppLaunches_ShowsHomePageContent() => Run(app =>
    {
        app.ResetToHome();

        // The page's own content, not its title. HomePage renders three status cards; asserting
        // on those means a broken page body fails here, which the old title check did not.
        Assert.True(app.Home.HasDiscoveryCard, "Home is missing the discovery status card");
        Assert.True(app.Home.HasViewerCard,    "Home is missing the viewer status card");
        Assert.True(app.Home.HasOutputCard,    "Home is missing the output status card");
        Assert.False(string.IsNullOrWhiteSpace(app.Home.DiscoveryStatus), "Discovery status is blank");
    });

    [SkippableFact]
    public void Navigation_ToSettingsAndBackToHome_ShowsEachPage() => Run(app =>
    {
        // Starts from a known page: the session is shared, so without this the test inherits
        // whatever page and orientation the previous test left behind.
        app.ResetToHome();

        app.Navigation.GoTo(NavDestination.Settings);
        app.Settings.WaitUntilVisible();

        // Back via the Home nav item, not the system Back button — Back closes a Shell app.
        app.Navigation.GoTo(NavDestination.Home);
        app.Home.WaitUntilVisible();
    });

    [SkippableFact]
    public void Navigation_WatchOnASourceRow_OpensTheViewer() => Run(app =>
    {
        app.ResetToHome();
        app.Navigation.GoTo(NavDestination.View);
        app.Sources.WaitUntilVisible();

        // A genuine unmet precondition rather than a masked failure: the CI emulator has no NDI
        // sources on its network, so the list renders its empty view and there is no row to tap.
        // Making this journey actually execute in CI is the point of #315.
        Skip.If(app.Sources.SourceCount == 0,
            "No NDI sources discovered on this network; the discover-to-watch journey needs a source.");

        app.Sources.WatchSource();

        app.Viewer.WaitUntilVisible();
        Assert.True(app.Viewer.HasVideoSurface, "The viewer opened without a video surface");
    });

    [SkippableFact]
    public void AdaptiveNavigation_InPortrait_PlacesNavigationAtTheBottom() => Run(app =>
    {
        app.Rotate(ScreenOrientation.Portrait);

        var home = app.Navigation.Item(NavDestination.Home);
        var window = app.WindowSize;

        Assert.True(home.Location.Y > window.Height * 0.70,
            $"Expected the Home nav item near the bottom in portrait. y={home.Location.Y}, height={window.Height}");
    });

    [SkippableFact]
    public void AdaptiveNavigation_InLandscape_PlacesNavigationInTheLeftRail() => Run(app =>
    {
        app.Rotate(ScreenOrientation.Landscape);

        var home = app.Navigation.Item(NavDestination.Home);
        var window = app.WindowSize;

        Assert.True(home.Location.X < window.Width * 0.20,
            $"Expected the Home nav item near the left edge in landscape. x={home.Location.X}, width={window.Width}");
        Assert.True(home.Location.Y < window.Height * 0.60,
            $"Expected the Home nav item in the left rail, not the bottom bar. y={home.Location.Y}, height={window.Height}");
    });

    [SkippableFact]
    public void AdaptiveNavigation_AllFourDestinations_ShowTheirOwnPage() => Run(app =>
    {
        app.Rotate(ScreenOrientation.Portrait);

        // Each destination is confirmed by its page's own root id, so a navigation that lands
        // somewhere unexpected fails here rather than passing on a shared string.
        app.Navigation.GoTo(NavDestination.Home);
        app.Home.WaitUntilVisible();

        app.Navigation.GoTo(NavDestination.Stream);
        app.Output.WaitUntilVisible();

        app.Navigation.GoTo(NavDestination.View);
        app.Sources.WaitUntilVisible();

        app.Navigation.GoTo(NavDestination.Settings);
        app.Settings.WaitUntilVisible();
    });

    [SkippableFact]
    public void Settings_AllFiveSections_AreReachable() => Run(app =>
    {
        app.ResetToHome();
        app.Navigation.GoTo(NavDestination.Settings);
        app.Settings.WaitUntilVisible();

        // Opening each section proves more than the old "the caption is on screen" check: the
        // panel has to actually render, which catches a section button wired to nothing.
        foreach (var section in Enum.GetValues<SettingsSection>())
        {
            app.Settings.OpenSection(section);
            Assert.True(app.Settings.IsSectionOpen(section), $"The {section} panel did not open");
        }
    });

    [SkippableFact]
    public void Settings_DiscoveryHost_SurvivesAnAppRestart() => Run(app =>
    {
        // Typing into the add-server Entry proves nothing: AddDiscoveryServerAsync clears it
        // before PersistAsync, so what actually survives a restart is a row in the discovery
        // server list. 10.255.255.1 is a non-routable address that will never collide with a
        // real server; the unusual port keeps cleanup unambiguous.
        const string host = "10.255.255.1";
        const string port = "45959";
        var endpoint = $"{host}:{port}";

        app.ResetToHome();
        app.Navigation.GoTo(NavDestination.Settings);
        app.Settings.WaitUntilVisible();
        app.Settings.OpenSection(SettingsSection.Discovery);

        var baseline = app.Settings.ServerRowCount;
        app.Settings.AddServer(host, port);

        try
        {
            // The row is added to the collection before PersistAsync is awaited, so it is not a
            // save barrier. Leaving Settings and coming back re-runs LoadCommand from the
            // repository, which is.
            app.Navigation.GoTo(NavDestination.Home);
            app.Home.WaitUntilVisible();
            app.Navigation.GoTo(NavDestination.Settings);
            app.Settings.WaitUntilVisible();
            app.Settings.OpenSection(SettingsSection.Discovery);

            Assert.Contains(endpoint, app.Settings.ServerRowEndpoints);

            Skip.IfNot(app.TryRestart(), "App lifecycle commands are unavailable in this environment.");

            app.Navigation.GoTo(NavDestination.Settings);
            app.Settings.WaitUntilVisible();
            app.Settings.OpenSection(SettingsSection.Discovery);

            Assert.Contains(endpoint, app.Settings.ServerRowEndpoints);
        }
        finally
        {
            // Cleanup by endpoint text depends on the very row locator a row template overflowing
            // its container can make disappear, which silently no-oped and left this test's bogus
            // server persisted. Counting rows down to the pre-test baseline does not depend on the
            // row's content rendering at all.
            app.Settings.RemoveServersDownTo(baseline);
        }
    });

    [SkippableFact]
    public void Settings_DiscoveryServerRow_RendersEveryControl() => Run(app =>
    {
        // Regression guard for a row template overflowing its container: on a narrow detail panel
        // the endpoint, Enabled switch and Up button dropped out of the accessibility tree
        // entirely (Android omits zero-area nodes rather than reporting them as present-but-tiny).
        // This is the one assertion that would have caught that directly instead of surfacing as
        // an opaque "Collection: []" three steps downstream.
        const string host = "10.255.255.2";
        const string port = "45960";

        app.ResetToHome();
        app.Navigation.GoTo(NavDestination.Settings);
        app.Settings.WaitUntilVisible();
        app.Settings.OpenSection(SettingsSection.Discovery);

        var baseline = app.Settings.ServerRowCount;
        app.Settings.AddServer(host, port);

        try
        {
            string[] everyControl =
            [
                TestIds.SettingsServerRowEndpoint,
                TestIds.SettingsServerRowEnabled,
                TestIds.SettingsServerRowUp,
                TestIds.SettingsServerRowDown,
                TestIds.SettingsServerRowEdit,
                TestIds.SettingsServerRowDelete,
            ];

            foreach (var id in everyControl)
            {
                var size = app.Settings.LastServerRowControlSize(id);
                Assert.True(size is { Width: > 0, Height: > 0 },
                    $"'{id}' is missing from the discovery server row or has zero area — the row " +
                    "template is overflowing its container.");
            }

            string[] clickableControls =
            [
                TestIds.SettingsServerRowEnabled,
                TestIds.SettingsServerRowUp,
                TestIds.SettingsServerRowDown,
                TestIds.SettingsServerRowEdit,
                TestIds.SettingsServerRowDelete,
            ];

            var minTouchTargetPx = app.Metrics.ToPixels(AccessibilityAudit.MinTouchTargetDp);

            foreach (var id in clickableControls)
            {
                var size = app.Settings.LastServerRowControlSize(id);
                Assert.True(size.Width >= minTouchTargetPx && size.Height >= minTouchTargetPx,
                    $"'{id}' is {size.Width}x{size.Height}px, below the " +
                    $"{AccessibilityAudit.MinTouchTargetDp}dp ({minTouchTargetPx}x{minTouchTargetPx}px) " +
                    "minimum touch target.");
            }
        }
        finally
        {
            app.Settings.RemoveServersDownTo(baseline);
        }
    });
}

[CollectionDefinition("AppiumSession")]
public sealed class AppiumSessionCollection : ICollectionFixture<AppiumDriverFixture> { }
