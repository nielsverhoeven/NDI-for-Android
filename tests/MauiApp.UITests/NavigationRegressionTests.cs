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
/// Tapping an item in the landscape rail intermittently exited the app to the launcher.
/// <c>ApplyPlacement</c> hid the outgoing <c>PrimaryTabBar</c> immediately and deferred the move
/// onto the matching <c>*-rail</c> route to a later dispatcher turn, so Shell briefly had no
/// visible current item — and a tap arriving in that window finished the activity.
/// </para>
/// <para>
/// The existing suite caught this, but only incidentally: five unrelated tests failed because they
/// happened to navigate in landscape, which is why the cause took four CI runs to isolate. This
/// file makes the property explicit, so the next occurrence names itself.
/// </para>
/// <para>
/// <b>Timing is the whole test.</b> The defect was a race, not a broken route — across four runs
/// the identical tap removed the app three times and survived once. So this taps immediately after
/// rotating, in both directions, rather than letting the app settle first: a version that waited
/// would pass against the bug.
/// </para>
/// </remarks>
[Collection("AppiumSession")]
public sealed class NavigationRegressionTests : UiTestBase
{
    private readonly ITestOutputHelper _output;

    public NavigationRegressionTests(AppiumDriverFixture fixture, ITestOutputHelper output)
        : base(fixture) => _output = output;

    [SkippableTheory]
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
                $"foreground — '{app.ForegroundPackage}' is. This is the #321 failure mode: " +
                "changing placement retires the outgoing navigation before the incoming route is " +
                "live, and a tap landing in that window finishes the activity.");
        }
    });

    private static ScreenOrientation Opposite(ScreenOrientation orientation) =>
        orientation == ScreenOrientation.Landscape
            ? ScreenOrientation.Portrait
            : ScreenOrientation.Landscape;
}
