using OpenQA.Selenium;
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
        const string host = "persist.test.local";

        app.ResetToHome();
        app.Navigation.GoTo(NavDestination.Settings);
        app.Settings.WaitUntilVisible();

        app.Settings.OpenSection(SettingsSection.Discovery);
        app.Settings.DiscoveryHost = host;
        app.Settings.Apply();
        app.Settings.WaitForApplied();

        Skip.IfNot(app.TryRestart(), "App lifecycle commands are unavailable in this environment.");

        app.Navigation.GoTo(NavDestination.Settings);
        app.Settings.WaitUntilVisible();
        app.Settings.OpenSection(SettingsSection.Discovery);

        Assert.Equal(host, app.Settings.DiscoveryHost);
    });
}

[CollectionDefinition("AppiumSession")]
public sealed class AppiumSessionCollection : ICollectionFixture<AppiumDriverFixture> { }
