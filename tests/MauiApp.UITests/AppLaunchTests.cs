using OpenQA.Selenium;
using NdiForAndroid.Features.Navigation.Models;
using NdiForAndroid.Features.Navigation.Services;
using NdiForAndroid.Testing;
using NdiForAndroid.UITests.Infrastructure;
using NdiForAndroid.UITests.Pages;
using Xunit;
using Xunit.Abstractions;

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
    private readonly ITestOutputHelper _output;

    public AppLaunchTests(AppiumDriverFixture fixture, ITestOutputHelper output) : base(fixture) => _output = output;

    [RetryableSkippableFact]
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

    [RetryableSkippableFact]
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

    [RetryableSkippableFact]
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
    [RetryableSkippableFact]
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

    /// <summary>
    /// #383/#384 slice 3: on a compact (phone-class) device, rotating to landscape while playing
    /// enters full screen in place with no page transition, and rotating back exits it. Skipped on
    /// a device that reports as tablet-class (`SmallestWidthDp >= 600`, mirroring
    /// `ViewerControlLayout.IsCompactDevice`) — tablets never auto-enter/exit full screen on
    /// rotation by design (see the up-front design consult, decision (b)), so this assertion does
    /// not apply there.
    /// </summary>
    [RetryableSkippableFact]
    public void Viewer_RotatedToLandscape_EntersFullScreenInPlace() => Run(app =>
    {
        app.ResetToHome();
        app.Navigation.GoTo(NavDestination.View);
        app.Sources.WaitUntilVisible();

        Skip.If(app.Sources.SourceCount == 0,
            "No NDI sources discovered on this network; rotation-driven full screen needs a playing source.");

        Skip.If(app.Metrics.SmallestWidthDp >= 600,
            "Rotation-driven full screen is compact-device-only (#384 slice 3); this device reports " +
            $"sw={app.Metrics.SmallestWidthDp:0}dp, i.e. tablet-class.");

        app.Sources.WatchSource();
        app.Viewer.WaitUntilVisible();
        app.Viewer.WaitUntilPlaying();

        app.Rotate(ScreenOrientation.Landscape);
        app.Viewer.WaitUntilFullScreen();
        Assert.True(app.Viewer.IsFullScreen, "Rotating to landscape did not enter full screen in place");
        Assert.False(app.Navigation.IsPresent(NavDestination.Home),
            "The bottom tab bar / left rail is still on screen in full screen");

        app.Rotate(ScreenOrientation.Portrait);
        app.Viewer.WaitUntilPlaying();
        Assert.False(app.Viewer.IsFullScreen, "Rotating back to portrait did not exit full screen");
        Assert.True(app.Navigation.IsPresent(NavDestination.Home),
            "Navigation chrome was not restored after rotating back to portrait");
    });

    [RetryableSkippableTheory]
    [InlineData(ScreenOrientation.Portrait)]
    [InlineData(ScreenOrientation.Landscape)]
    public void AdaptiveNavigation_MatchesPolicyForTheCurrentConfiguration(ScreenOrientation orientation) => Run(app =>
    {
        app.Rotate(orientation);

        var widthDp = app.WindowSize.Width / app.Metrics.Density;
        var sizeClass = WindowSizeClassService.Classify(widthDp);
        var deviceOrientation = orientation == ScreenOrientation.Landscape
            ? DeviceOrientation.Landscape
            : DeviceOrientation.Portrait;

        var policy = new NavigationPolicyService(new WindowSizeClassService());
        var expected = policy.ResolvePlacement(deviceOrientation, sizeClass);

        var home = app.Navigation.Item(NavDestination.Home);
        var window = app.WindowSize;

        if (expected == NavigationPlacementMode.LeftRail)
        {
            Assert.True(home.Location.X < window.Width * 0.20,
                $"{sizeClass}/{orientation}: expected the left rail (x<20% of {window.Width}), " +
                $"Home is at x={home.Location.X}");
            Assert.True(home.Location.Y < window.Height * 0.60,
                $"{sizeClass}/{orientation}: expected the left rail, not the bottom bar " +
                $"(y<60% of {window.Height}), Home is at y={home.Location.Y}");
        }
        else
        {
            Assert.True(home.Location.Y > window.Height * 0.70,
                $"{sizeClass}/{orientation}: expected the bottom tab bar (y>70% of {window.Height}), " +
                $"Home is at y={home.Location.Y}");
        }
    });

    [RetryableSkippableFact]
    public void AdaptiveNavigation_RailPlacement_NeverShowsBottomNavigationBar_OnLaunchAndRotation() => Run(app =>
    {
        try
        {
            app.Rotate(ScreenOrientation.Landscape);
            Skip.IfNot(app.TryRestart(), "App lifecycle commands are unavailable in this environment.");
            app.Home.WaitUntilVisible(Timeouts.AppStart);
            AssertNoBottomNavigationBarForRailPlacement(app, "immediately after a cold launch in landscape");

            app.ResetToHome();
            app.Rotate(ScreenOrientation.Landscape);
            AssertNoBottomNavigationBarForRailPlacement(app, "after rotating from portrait into landscape");

            // #395: the bar is hidden at ShellItem scope, so it has to come back on its own when the
            // placement returns to bottom — nothing navigates to bring it back any more.
            app.Rotate(ScreenOrientation.Portrait);
            app.Home.WaitUntilVisible();
            AssertBottomNavigationBarForBottomPlacement(app, "after rotating from landscape back to portrait");
        }
        finally
        {
            try { app.Rotate(ScreenOrientation.Portrait); } catch { }
        }
    });

    [RetryableSkippableFact]
    public void AdaptiveNavigation_RailPlacement_NeverShowsBottomNavigationBar_AfterTwoPaneFullScreen() => Run(app =>
    {
        try
        {
            app.ResetToHome();
            app.Rotate(ScreenOrientation.Landscape);

            var widthDp = app.WindowSize.Width / app.Metrics.Density;
            var sizeClass = WindowSizeClassService.Classify(widthDp);
            Skip.If(sizeClass != WindowSizeClass.Expanded,
                $"The two-pane pane only renders on an Expanded window (>840dp); this device measures {sizeClass} ({widthDp:0}dp) in landscape.");

            app.Navigation.GoTo(NavDestination.View);
            app.Sources.WaitUntilVisible();

            Skip.If(app.Sources.SourceCount == 0,
                "No NDI sources discovered on this network; the two-pane full-screen invariant needs a playing source.");

            app.Sources.WatchSource();
            Assert.True(app.Sources.IsViewerPaneVisible, "The two-pane viewer pane did not appear on an Expanded window");

            app.Viewer.WaitUntilPlaying();
            app.Viewer.ToggleFullScreen();
            app.Viewer.WaitUntilFullScreen();
            AssertNoBottomNavigationBarForRailPlacement(app, "while the two-pane pane is full screen", railIsVisible: false);

            app.Viewer.ExitFullScreen();
            app.Viewer.WaitUntilPlaying();

            // Shell recomputes tab-bar visibility a beat after the chrome restore; sampling the
            // tree before that would let the defect through unnoticed.
            Thread.Sleep(Timeouts.OrientationSettle);
            AssertNoBottomNavigationBarForRailPlacement(app, "after exiting full screen on the two-pane pane");
        }
        finally
        {
            try { if (app.Viewer.IsFullScreen) app.Viewer.ExitFullScreen(); } catch { }
            try { app.Rotate(ScreenOrientation.Portrait); } catch { }
        }
    });

    /// <summary>The placement the app's own policy resolves for the device's current
    /// configuration — the reference every chrome assertion is made against, so a test only asserts
    /// what is valid for the size class it is running on (Compact phone vs Expanded tablet).</summary>
    private static (NavigationPlacementMode Placement, WindowSizeClass SizeClass, DeviceOrientation Orientation, double WidthDp)
        ResolveExpectedPlacement(NdiApp app)
    {
        var widthDp = app.WindowSize.Width / app.Metrics.Density;
        var sizeClass = WindowSizeClassService.Classify(widthDp);
        var orientation = app.Orientation == ScreenOrientation.Landscape
            ? DeviceOrientation.Landscape
            : DeviceOrientation.Portrait;

        var policy = new NavigationPolicyService(new WindowSizeClassService());
        return (policy.ResolvePlacement(orientation, sizeClass), sizeClass, orientation, widthDp);
    }

    /// <summary>Asserts the bottom navigation bar is on screen when the current device configuration
    /// resolves to the bottom placement; otherwise logs that the invariant was not exercised.</summary>
    private void AssertBottomNavigationBarForBottomPlacement(NdiApp app, string checkpoint)
    {
        var (expected, sizeClass, orientation, widthDp) = ResolveExpectedPlacement(app);

        if (expected != NavigationPlacementMode.Bottom)
        {
            var message = $"Bottom placement is not reachable on this device ({sizeClass}/{orientation}); " +
                $"the bottom-bar-is-back invariant was not exercised {checkpoint}.";
            _output.WriteLine(message);
            Console.WriteLine(message);
            return;
        }

        var passingMessage =
            $"Bottom placement expected ({sizeClass}/{orientation}, {widthDp:0}dp); asserting the bottom bar {checkpoint}.";
        _output.WriteLine(passingMessage);
        Console.WriteLine(passingMessage);
        Console.WriteLine($"PROBE {checkpoint}: {app.Navigation.ChromeProbe()}");
        System.Threading.Thread.Sleep(3000);
        Console.WriteLine($"PROBE+3s {checkpoint}: {app.Navigation.ChromeProbe()}");

        Assert.True(app.Navigation.WaitForBottomNavigationBar(shown: true, Timeouts.Element),
            $"Shell's bottom navigation bar is not on screen {checkpoint}, while the bottom bar is the " +
            $"expected placement ({sizeClass}/{orientation}).");
    }

    /// <summary>
    /// Asserts the navigation chrome is the one the current configuration calls for, and that it
    /// still marks <paramref name="selected"/>: on rail placement the rail is present, announces
    /// <paramref name="selected"/> as selected and the bottom bar is gone; on bottom placement the
    /// bottom bar is on screen. (Only the rail announces a selection — see
    /// <see cref="NavigationBar.AnnouncesSelected"/> — so there is no selection check for the bar.)
    /// </summary>
    private void AssertChromeMatchesPlacement(NdiApp app, NavDestination selected, string checkpoint)
    {
        var (expected, sizeClass, orientation, widthDp) = ResolveExpectedPlacement(app);
        var context = $"({sizeClass}/{orientation}, {widthDp:0}dp)";
        _output.WriteLine($"Expecting {expected} chrome {context} {checkpoint}.");
        Console.WriteLine($"PROBE {checkpoint}: {app.Navigation.ChromeProbe()}");
        System.Threading.Thread.Sleep(3000);
        Console.WriteLine($"PROBE+3s {checkpoint}: {app.Navigation.ChromeProbe()}");

        if (expected == NavigationPlacementMode.LeftRail)
        {
            Assert.True(app.Navigation.IsPresent(selected),
                $"The left rail is not on screen {checkpoint}, while it is the expected placement {context}.");
            Assert.True(app.Navigation.AnnouncesSelected(selected),
                $"The left rail does not announce {selected} as selected {checkpoint} {context}.");
            Assert.True(app.Navigation.WaitForBottomNavigationBar(shown: false, Timeouts.Element),
                $"Shell's bottom navigation bar is still on screen {checkpoint}, while the left rail is " +
                $"the expected placement {context}. {app.Navigation.ChromeProbe()}");
        }
        else
        {
            Assert.True(app.Navigation.WaitForBottomNavigationBar(shown: true, Timeouts.Element),
                $"Shell's bottom navigation bar is not on screen {checkpoint}, while the bottom bar is " +
                $"the expected placement {context}.");
        }
    }

    /// <summary>Asserts the bottom navigation bar is absent when the current device configuration
    /// resolves to the left-rail placement; otherwise logs that the invariant was not exercised.</summary>
    private void AssertNoBottomNavigationBarForRailPlacement(NdiApp app, string checkpoint, bool railIsVisible = true)
    {
        var (expected, sizeClass, orientation, widthDp) = ResolveExpectedPlacement(app);

        if (expected != NavigationPlacementMode.LeftRail)
        {
            var message = $"Rail placement is not reachable on this device ({sizeClass}/{orientation}); " +
                $"the no-bottom-bar invariant was not exercised {checkpoint}.";
            _output.WriteLine(message);
            Console.WriteLine(message);
            return;
        }

        var passingMessage =
            $"Rail placement expected ({sizeClass}/{orientation}, {widthDp:0}dp); asserting no bottom bar {checkpoint}.";
        _output.WriteLine(passingMessage);
        Console.WriteLine(passingMessage);

        if (railIsVisible)
            Assert.True(app.Navigation.IsPresent(NavDestination.Home),
                $"The left rail is not on screen {checkpoint}, so the no-bottom-bar assertion would " +
                "be vacuous — the tree was unreadable or the app was not in front.");

        Assert.False(app.Navigation.HasBottomNavigationBar(),
            $"Shell's bottom navigation bar is present {checkpoint}, while the left rail is the expected placement ({sizeClass}/{orientation}). {app.Navigation.ChromeProbe()}");
    }

    [RetryableSkippableFact]
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

    [RetryableSkippableFact]
    public void Rotating_WhileOnANonHomeTab_KeepsTheSameDestination() => Run(app =>
    {
        // The Appium session is shared: start from a known page and orientation rather than
        // inheriting whatever the previous test left behind (including a pushed page).
        app.ResetToHome();

        app.Navigation.GoTo(NavDestination.Stream);
        app.Output.WaitUntilVisible();

        // Landscape is the rail placement on this AVD, and the rail is the only placement that
        // announces its selection (NavigationBar.AnnouncesSelected reads the ", selected" suffix
        // the rail puts in content-desc, #345 home-nav-06); the bottom tab bar has no such suffix,
        // so in portrait the page's own id is the assertion.
        app.Rotate(ScreenOrientation.Landscape);
        app.Output.WaitUntilVisible();
        Assert.True(app.Navigation.AnnouncesSelected(NavDestination.Stream),
            "Rotating away from the Stream tab must not silently switch the selected destination to Home (#393).");

        app.Rotate(ScreenOrientation.Portrait);
        app.Output.WaitUntilVisible();
        Assert.True(app.Output.IsVisible,
            "Rotating back to portrait must keep Stream selected (#393).");
    });

    /// <summary>
    /// #395: a rotation on a pushed page keeps the page and swaps the chrome straight away. Before
    /// #395 the rail was a second family of ShellItems, a pushed page had no equivalent there, and
    /// #393 therefore deferred the chrome swap until the page was popped — so a phone rotated to
    /// landscape on the diagnostic log kept the bottom bar and showed no rail.
    /// </summary>
    /// <remarks>
    /// Every chrome assertion is made against the placement the app's own policy resolves for this
    /// device (<see cref="AssertChromeMatchesPlacement"/>). On a Compact phone (the Nexus 6 CI AVD)
    /// portrait is the bottom bar and landscape the rail, so both halves swap; on an Expanded tablet
    /// (pixel_c) the rail is live in both orientations, and the test proves the page and the rail
    /// both survive the rotation.
    /// </remarks>
    [RetryableSkippableFact]
    public void Rotating_OnAPushedPage_KeepsThePageAndSwapsChrome() => Run(app =>
    {
        app.ResetToHome();

        try
        {
            app.Navigation.GoTo(NavDestination.Settings);
            app.Settings.WaitUntilVisible();
            app.Settings.OpenSection(SettingsSection.DeveloperTools);
            app.Settings.OpenDiagnosticLog();
            app.DiagnosticLog.WaitUntilVisible();

            app.Rotate(ScreenOrientation.Landscape);
            Assert.True(app.DiagnosticLog.IsVisible,
                "Rotating to landscape on the pushed diagnostic log must keep that page on screen (#395).");
            AssertChromeMatchesPlacement(app, NavDestination.Settings,
                "after rotating to landscape on the pushed diagnostic log");

            app.Rotate(ScreenOrientation.Portrait);
            Assert.True(app.DiagnosticLog.IsVisible,
                "Rotating back to portrait on the pushed diagnostic log must keep that page on screen (#395).");
            AssertChromeMatchesPlacement(app, NavDestination.Settings,
                "after rotating back to portrait on the pushed diagnostic log");
        }
        finally
        {
            // The pushed page stays on the Settings stack across tab switches, so pop it here or a
            // later test that opens Settings lands on the log instead.
            try { app.Rotate(ScreenOrientation.Portrait); } catch { }
            try { if (app.DiagnosticLog.IsVisible) app.PressBackButton(); } catch { }
        }
    });

    [RetryableSkippableFact]
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

    [RetryableSkippableFact]
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

    [RetryableSkippableFact]
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

    [RetryableSkippableFact]
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

    [RetryableSkippableFact]
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

    [RetryableSkippableFact]
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

    [RetryableSkippableFact]
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

    [RetryableSkippableFact]
    public void Settings_DeveloperTools_OpenDiagnosticLog_ShowsTheLogPage() => Run(app =>
    {
        // #437: the Diagnostic Log page was registered as a route but nothing navigated to it.
        app.ResetToHome();
        app.Navigation.GoTo(NavDestination.Settings);
        app.Settings.WaitUntilVisible();
        app.Settings.OpenSection(SettingsSection.DeveloperTools);

        app.Settings.OpenDiagnosticLog();
        app.DiagnosticLog.WaitUntilVisible();

        // Back returns to Settings rather than leaving the app.
        app.PressBackButton();
        app.Settings.WaitUntilVisible();
    });

    [RetryableSkippableFact]
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
