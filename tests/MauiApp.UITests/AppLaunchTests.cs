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

    /// <summary>
    /// Every assertion in this test is preceded by an explicit wait, because both
    /// <c>PageObject.IsPresent</c> and <c>NavigationBar.IsPresent</c> deliberately do not wait:
    /// <c>WaitUntilFullScreen()</c> after each enter (blocks on the overlay-exclusive
    /// `viewer.fullScreen.qualityCycle`), <c>WaitUntilPlaying()</c> after each exit (blocks on
    /// `viewer.stop`, i.e. the Deck/Sheet layout has actually re-rendered). This is required
    /// change 4's anti-race measure applied to all four transitions, not only to the Back-button
    /// one.
    /// </summary>
    [SkippableFact]
    public void FullScreen_EnterViaButton_HidesChromeAndExitButtonWorks() => Run(app =>
    {
        app.ResetToHome();
        app.Navigation.GoTo(NavDestination.View);
        app.Sources.WaitUntilVisible();

        Skip.If(app.Sources.SourceCount == 0,
            "No NDI sources discovered on this network; full screen needs a playing source.");

        app.Sources.WatchSource();
        app.Viewer.WaitUntilVisible();
        app.Viewer.WaitUntilPlaying();

        app.Viewer.ToggleFullScreen();
        app.Viewer.WaitUntilFullScreen();
        Assert.True(app.Viewer.IsFullScreen, "Full screen did not engage after the toggle button");
        Assert.False(app.Navigation.IsPresent(NavDestination.Home),
            "The bottom tab bar / left rail is still on screen in full screen");

        app.Viewer.ExitFullScreen();
        app.Viewer.WaitUntilPlaying();
        Assert.False(app.Viewer.IsFullScreen, "Full screen did not exit after the overlay's exit button");
        Assert.True(app.Navigation.IsPresent(NavDestination.Home),
            "Navigation chrome was not restored after exiting full screen");

        app.Viewer.ToggleFullScreen();
        app.Viewer.WaitUntilFullScreen();
        Assert.True(app.Viewer.IsFullScreen, "Full screen did not re-engage for the Back-button check");

        app.PressBackButton();
        app.Viewer.WaitUntilPlaying();
        Assert.False(app.Viewer.IsFullScreen, "Back button did not exit full screen");
        Assert.True(app.Navigation.IsPresent(NavDestination.Home),
            "Navigation chrome was not restored after Back exited full screen");
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
    public void Stream_TypedStreamName_SurvivesATabSwitch() => Run(app =>
    {
        app.ResetToHome();

        app.Navigation.GoTo(NavDestination.Stream);
        app.Output.WaitUntilVisible();
        app.Output.StreamName = "E2E-Keep-Me";

        // Leaving and re-entering the tab used to bind a brand-new OutputViewModel (#352/#359):
        // the constructor default "NDI-Android" came back and the typed name was lost. Nothing in
        // this suite ever starts output, so no PreferredStreamName is persisted that LoadCommand
        // could legitimately apply on re-entry.
        app.Navigation.GoTo(NavDestination.Home);
        app.Home.WaitUntilVisible();
        app.Navigation.GoTo(NavDestination.Stream);
        app.Output.WaitUntilVisible();

        Assert.Equal("E2E-Keep-Me", app.Output.StreamName);
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
            Assert.True(app.Settings.IsSectionSelected(section),
                $"The {section} rail button does not announce itself as selected (#345 SET-1)");
        }
    });

    [SkippableFact]
    public void Settings_CompactRail_SectionButtonsMeet48dp() => Run(app =>
    {
        // The Nexus 6 API 35 CI AVD is 411 dp wide in portrait — Compact — so this exercises
        // CompactRail; on a Medium/Expanded window it measures VerticalRail instead, which is
        // also styled with the 48 dp minimum, so the test is never vacuous either way (#370 P-3).
        app.ResetToHome();
        app.Rotate(ScreenOrientation.Portrait);
        app.Navigation.GoTo(NavDestination.Settings);
        app.Settings.WaitUntilVisible();

        var minPx = app.Metrics.ToPixels(AccessibilityAudit.MinTouchTargetDp);

        foreach (var section in Enum.GetValues<SettingsSection>())
        {
            var height = app.Settings.SectionButtonHeightPx(section);
            Assert.True(height >= minPx,
                $"{section} section button is {height}px tall, below the 48 dp minimum ({minPx}px) — #370 P-3");
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
    public void Settings_DiscoveryEmptyState_TracksTheServerList() => Run(app =>
    {
        const string host = "10.255.255.2";
        const string port = "45960";
        var endpoint = $"{host}:{port}";

        app.ResetToHome();
        app.Navigation.GoTo(NavDestination.Settings);
        app.Settings.WaitUntilVisible();
        app.Settings.OpenSection(SettingsSection.Discovery);

        var baseline = app.Settings.ServerRowCount;
        Assert.Equal(baseline == 0, app.Settings.IsDiscoveryEmptyStateShown);

        app.Settings.AddServer(host, port);
        Assert.Contains(endpoint, app.Settings.ServerRowEndpoints);
        Assert.False(app.Settings.IsDiscoveryEmptyStateShown);

        try
        {
            // The confirmation dialog (#347) must not block the empty-state read-back: decline it
            // once to prove the row survives, then accept it to actually remove the row.
            app.Settings.RemoveServer(endpoint); // clicks Delete then confirms
        }
        finally
        {
            app.Settings.RemoveServersDownTo(baseline);
        }

        if (baseline == 0)
            Assert.True(app.Settings.IsDiscoveryEmptyStateShown);
    });

    [SkippableFact]
    public void Settings_DeleteServer_DeclinedConfirmation_LeavesTheRow() => Run(app =>
    {
        const string host = "10.255.255.3";
        const string port = "45961";
        var endpoint = $"{host}:{port}";

        app.ResetToHome();
        app.Navigation.GoTo(NavDestination.Settings);
        app.Settings.WaitUntilVisible();
        app.Settings.OpenSection(SettingsSection.Discovery);

        var baseline = app.Settings.ServerRowCount;
        app.Settings.AddServer(host, port);

        try
        {
            Assert.Contains(endpoint, app.Settings.ServerRowEndpoints);

            app.Settings.TapDeleteForRow(endpoint);
            app.Settings.CancelDeleteServer();

            app.Settings.WaitForServerRow(endpoint);
            Assert.Contains(endpoint, app.Settings.ServerRowEndpoints);
        }
        finally
        {
            app.Settings.RemoveServersDownTo(baseline);
        }
    });

    [SkippableFact]
    public void Settings_DeveloperMode_TappingTheLabel_TogglesTheSwitch() => Run(app =>
    {
        app.ResetToHome();
        app.Navigation.GoTo(NavDestination.Settings);
        app.Settings.WaitUntilVisible();
        app.Settings.OpenSection(SettingsSection.DeveloperTools);

        var before = app.Settings.IsDeveloperModeOn;
        try
        {
            app.Settings.ToggleDeveloperModeViaLabel();
            Assert.NotEqual(before, app.Settings.IsDeveloperModeOn);
        }
        finally
        {
            // Restore, whichever way the assertion landed, so later tests see the original state.
            if (app.Settings.IsDeveloperModeOn != before)
                app.Settings.ToggleDeveloperModeViaLabel();
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
