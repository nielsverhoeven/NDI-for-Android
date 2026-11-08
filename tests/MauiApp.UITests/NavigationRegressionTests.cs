using OpenQA.Selenium;
using NdiForAndroid.UITests.Infrastructure;
using NdiForAndroid.UITests.Pages;
using Xunit;
using Xunit.Abstractions;

namespace NdiForAndroid.UITests;

/// <summary>
/// Regression cover for #321 — navigation that removed the app instead of navigating.
/// </summary>
/// <remarks>
/// <para>
/// Tapping an item in the landscape rail intermittently exited the app to the launcher. The
/// structural half is gone: a placement change no longer hides one <c>ShellItem</c> and navigates
/// to another (#393, PR #397), and the rail and the bottom bar are now two kinds of chrome over a
/// single <c>TabBar</c> (#395, PRs #446/#447), so there is no longer a window in which Shell has
/// no visible current item. <see cref="Navigation_TappingImmediatelyAfterRotating_KeepsTheAppOnScreen"/>
/// pins that down. <b>Timing is the whole test</b>: the defect was a race, so it taps immediately
/// after rotating, in both directions, rather than letting the app settle first — a version that
/// waited would have passed against the bug.
/// </para>
/// <para>
/// The other half is geometric, not structural: on a phone with 3-button navigation the
/// navigation bar takes the <em>left</em> edge under <c>ROTATION_270</c>, the rail is on that
/// same edge, and the window draws edge-to-edge. PR #324 measured the bar at x 0..168 and a rail
/// item's tap point at x=126 on the CI Nexus 6, and read the navigation-bar inset as 0. A tap
/// delivered inside the bar goes to the system, not the app — no crash, no
/// <c>finishActivity</c>, nothing in the activity log, because the app is never involved.
/// <see cref="Navigation_TappingRailBesideSideNavigationBar_KeepsTheAppOnScreen"/> drives exactly
/// that configuration through <see cref="SideNavigationBarScope"/>. It is excluded from the
/// default gate by its <c>Category</c> trait until a fix exists (architect ruling 2026-10-20, #321
/// item 7) and is dispatched on demand with a <c>test_filter</c>; the geometry it depends on is
/// measured by <c>SystemBarInsetTests.Navigation_RailItems_AreNotCoveredByTheNavigationBar</c>.
/// </para>
/// </remarks>
[Collection("AppiumSession")]
public sealed class NavigationRegressionTests : UiTestBase
{
    private readonly ITestOutputHelper _output;

    public NavigationRegressionTests(AppiumDriverFixture fixture, ITestOutputHelper output)
        : base(fixture) => _output = output;

    [RetryableSkippableTheory]
    [InlineData(ScreenOrientation.Landscape)]
    [InlineData(ScreenOrientation.Portrait)]
    public void Navigation_TappingImmediatelyAfterRotating_KeepsTheAppOnScreen(ScreenOrientation orientation) => Run(app =>
    {
        // Rotate from the other placement, so the run actually crosses the tab-bar/rail swap that
        // #321 lived in. Rotating to the orientation we are already in would change nothing and
        // the test would prove nothing.
        app.Rotate(Opposite(orientation));
        app.Navigation.GoTo(NavDestination.Home);
        app.Home.WaitUntilVisible();

        try
        {
            app.Rotate(orientation);

            // Home twice on purpose. Re-selecting the destination already showing was one of the
            // hypotheses ruled out while diagnosing this, and it costs one tap to keep covered.
            var journey = new[]
            {
                NavDestination.Stream,
                NavDestination.View,
                NavDestination.Home,
                NavDestination.Home,
                NavDestination.Settings,
            };

            foreach (var destination in journey)
            {
                // GoTo throws by itself if the tap removed the app, naming the placement and the
                // destination. The assertion below covers the app going away a moment later instead.
                app.Navigation.GoTo(destination);

                _output.WriteLine($"{orientation}/{destination}: resolved via {app.Navigation.LastResolution}");

                Assert.True(app.IsInForeground,
                    $"After tapping {destination} in {orientation} the app is no longer in the " +
                    $"foreground — '{app.ForegroundPackage}' is. This is the #321 failure mode: a " +
                    "placement change must swap chrome only and a rail tap must reach the app.");
            }
        }
        finally
        {
            // Other tests in this suite assume portrait.
            app.Rotate(ScreenOrientation.Portrait);
        }
    });

    [RetryableSkippableFact]
    [Trait("Category", SideNavigationBarScope.TraitCategory)]
    public void Navigation_TappingRailBesideSideNavigationBar_KeepsTheAppOnScreen() => Run(app =>
    {
        // 3-button navigation, bar docked on the left (ROTATION_270): the only configuration in
        // which system UI shares an edge with the rail. Skips on a device that cannot produce it.
        using var sideBar = SideNavigationBarScope.Enter(app, _output);

        _output.WriteLine($"navigation bar {sideBar.NavigationBar}; side-docked={sideBar.NavigationBar.IsSideDocked}");

        // Twenty consecutive taps, cycling every destination. One tap landing on a system button
        // is enough to leave the app, so the count is what makes a pass mean something: a rail
        // whose tap points sit inside the bar cannot survive twenty of them.
        const int taps = 20;
        var destinations = Enum.GetValues<NavDestination>();

        for (var i = 0; i < taps; i++)
        {
            var destination = destinations[(i + 1) % destinations.Length];

            // GoTo throws by itself when the tap removed the app from the screen.
            app.Navigation.GoTo(destination);

            Assert.True(app.IsInForeground,
                $"Tap {i + 1}/{taps} on the {destination} rail item, with the navigation bar at " +
                $"{sideBar.NavigationBar}, left the app — '{app.ForegroundPackage}' is in front. " +
                "This is the #321 geometric failure mode: the rail item's tap point is inside the " +
                "side navigation bar, so the press is delivered to the system instead of the app.");
        }

        _output.WriteLine($"{taps} rail taps beside the side navigation bar; the app stayed in the foreground.");
    });

    private static ScreenOrientation Opposite(ScreenOrientation orientation) =>
        orientation == ScreenOrientation.Landscape
            ? ScreenOrientation.Portrait
            : ScreenOrientation.Landscape;
}
