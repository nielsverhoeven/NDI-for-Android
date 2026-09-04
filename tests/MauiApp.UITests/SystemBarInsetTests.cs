using OpenQA.Selenium;
using NdiForAndroid.UITests.Pages;
using Xunit;
using Xunit.Abstractions;

namespace NdiForAndroid.UITests;

/// <summary>
/// The app's chrome stays clear of the system bars (#296, part of #313).
/// </summary>
/// <remarks>
/// <para>
/// The window is drawn edge-to-edge, so nothing keeps content out from under the status bar
/// automatically. #296 was the left rail's first item sitting behind the clock — obvious to
/// anyone who opened the app in landscape, invisible to a suite that only asked whether elements
/// could be found.
/// </para>
/// <para>
/// The status-bar height is read from the device on every assertion rather than assumed.
/// <see cref="Infrastructure.DeviceMetrics"/> throws if it cannot be read, because a height that
/// defaulted to 0 would turn this into "assert y >= 0" — which passes against the bug.
/// </para>
/// </remarks>
[Collection("AppiumSession")]
public sealed class SystemBarInsetTests : UiTestBase
{
    private readonly ITestOutputHelper _output;

    public SystemBarInsetTests(AppiumDriverFixture fixture, ITestOutputHelper output)
        : base(fixture) => _output = output;

    [SkippableTheory]
    [InlineData(ScreenOrientation.Portrait)]
    [InlineData(ScreenOrientation.Landscape)]
    public void Navigation_TopmostItem_SitsBelowTheStatusBar(ScreenOrientation orientation) => Run(app =>
    {
        app.Rotate(orientation);
        app.Navigation.GoTo(NavDestination.Home);
        app.Home.WaitUntilVisible();

        var statusBarHeight = app.Metrics.StatusBarHeight;

        // Whichever navigation item is highest on screen — in landscape that is the top of the
        // rail, which is where #296 bit. Asserting on the topmost item rather than on Home
        // specifically means the test keeps working if the destination order ever changes.
        var topmost = Enum.GetValues<NavDestination>()
            .Select(d => (Destination: d, Element: app.Navigation.Item(d)))
            .MinBy(x => x.Element.Location.Y);

        var top = topmost.Element.Location.Y;

        _output.WriteLine(
            $"{orientation}: status bar {statusBarHeight}px, topmost nav item " +
            $"({topmost.Destination}) at y={top}");

        Assert.True(top >= statusBarHeight,
            $"In {orientation} the topmost navigation item ({topmost.Destination}) starts at " +
            $"y={top}, inside the {statusBarHeight}px status bar. This is the #296 failure mode — " +
            "the window draws edge-to-edge and the rail is not inset, so its first item sits " +
            "under the clock.");
    });

    [SkippableFact]
    public void PageContent_DoesNotStartUnderTheStatusBar() => Run(app =>
    {
        app.Rotate(ScreenOrientation.Portrait);
        app.Navigation.GoTo(NavDestination.Home);
        app.Home.WaitUntilVisible();

        var statusBarHeight = app.Metrics.StatusBarHeight;
        var card = app.Home.DiscoveryCardBounds;

        _output.WriteLine($"status bar {statusBarHeight}px, first card at y={card.Y}");

        Assert.True(card.Y >= statusBarHeight,
            $"Home's first content card starts at y={card.Y}, inside the {statusBarHeight}px " +
            "status bar — page content is drawing under the system bar.");
    });
}
