using OpenQA.Selenium;
using NdiForAndroid.UITests.Infrastructure;
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

    /// <summary>
    /// #321, the same defect as #296 on a different edge. With 3-button navigation the bar leaves
    /// the bottom in landscape and takes a side — the left side under <c>ROTATION_270</c>; the
    /// rail is on that side; the window draws edge-to-edge. PR #324 measured the bar owning
    /// x 0..168 while a rail item spanned x 28..224 on the CI Nexus 6, so the centre a tap lands
    /// on (x=126) was underneath system UI and the press went to the system: the app went Home
    /// with no crash and nothing in the activity log, because it was never involved.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Asserts each item's <em>centre</em>, because that is where a tap is delivered. An edge
    /// assertion passes while the majority of the item — and the tap point — stays under the bar.
    /// </para>
    /// <para>
    /// Carries the <c>SideNavigationBar</c> trait and is therefore <b>not in the default gate</b>:
    /// <c>run-emulator-tests.sh</c> excludes the category when no filter is given. The architect's
    /// 2026-10-20 ruling (#321, item 7) defers porting this guard into the PR gate until a fix
    /// exists — a guard without a fix would turn the release gate red. Dispatch it on demand:
    /// <c>emulator-tests.yml</c> with
    /// <c>test_filter=FullyQualifiedName~Navigation_RailItems_AreNotCoveredByTheNavigationBar</c>.
    /// </para>
    /// <para>
    /// Measures without tapping. A tap that lands on the bar leaves the app, and the geometry is
    /// the diagnostic — the tap itself is exercised by
    /// <c>NavigationRegressionTests.Navigation_TappingRailBesideSideNavigationBar_KeepsTheAppOnScreen</c>.
    /// </para>
    /// </remarks>
    [RetryableSkippableFact]
    [Trait("Category", SideNavigationBarScope.TraitCategory)]
    public void Navigation_RailItems_AreNotCoveredByTheNavigationBar() => Run(app =>
    {
        using var sideBar = SideNavigationBarScope.Enter(app, _output);

        var bar = app.Metrics.NavigationBar;
        Skip.If(!bar.Visible, "No navigation bar is shown, so nothing can be covered by one.");

        var statusBarHeight = app.Metrics.StatusBarHeight;
        var density = app.Metrics.Density;
        _output.WriteLine(
            $"navigation bar {bar}; side-docked={bar.IsSideDocked}; status bar {statusBarHeight}px; " +
            $"density {density:0.##} ({app.Metrics.ToPixels(1)}px/dp); window {app.WindowSize.Width}x{app.WindowSize.Height}");

        var covered = new List<string>();
        var loggedContainer = false;

        foreach (var destination in Enum.GetValues<NavDestination>())
        {
            var element = app.Navigation.Item(destination, Timeouts.Navigation);
            var location = element.Location;
            var size = element.Size;

            // The centre, because that is where a tap is delivered.
            var centreX = location.X + size.Width / 2;
            var centreY = location.Y + size.Height / 2;

            _output.WriteLine(
                $"{destination}: {size.Width}x{size.Height} at ({location.X},{location.Y}), " +
                $"tap point ({centreX},{centreY}), resolved via {app.Navigation.LastResolution}" +
                (bar.Contains(centreX, centreY) ? " — INSIDE the navigation bar" : ""));

            if (!loggedContainer)
            {
                // The rail's ancestors decide which lever an inset fix needs (#321 hypothesis 3):
                // if the flyout container itself is pinned at x=0, padding on RailItems compresses
                // the items instead of moving them. Best effort — the hierarchy is diagnostic only.
                LogAncestors(element);
                loggedContainer = true;
            }

            if (bar.Contains(centreX, centreY))
                covered.Add($"{destination} tap point ({centreX},{centreY})");
        }

        Assert.True(covered.Count == 0,
            $"These rail items' tap points are inside the navigation bar ({bar}): " +
            $"{string.Join("; ", covered)}. A tap on them is delivered to the system instead of the " +
            "app. This is the #321 failure mode — the rail is not inset from the side navigation " +
            "bar, so tapping it goes Home.");
    });

    private void LogAncestors(IWebElement element)
    {
        try
        {
            var current = element;
            for (var depth = 1; depth <= 4; depth++)
            {
                current = current.FindElement(By.XPath(".."));
                var id = current.GetAttribute("resource-id");
                _output.WriteLine(
                    $"  ancestor {depth}: {current.TagName} {current.Size.Width}x{current.Size.Height} " +
                    $"at ({current.Location.X},{current.Location.Y})" +
                    (string.IsNullOrEmpty(id) ? "" : $" id={id}"));
            }
        }
        catch (Exception ex)
        {
            _output.WriteLine($"  (ancestor geometry unavailable: {ex.GetType().Name}: {ex.Message})");
        }
    }

    [RetryableSkippableTheory]
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

    [RetryableSkippableFact]
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
