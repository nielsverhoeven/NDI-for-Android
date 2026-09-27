using NdiForAndroid.Features.Navigation.Models;
using NdiForAndroid.Features.Navigation.Services;
using NdiForAndroid.Features.Navigation.ViewModels;
using Xunit;

namespace NdiForAndroid.Tests.Features.Navigation;

public sealed class PrimaryNavigationMetadataTests
{
    [Fact]
    public void Items_ContainExactlyFourPrimaryDestinations_WithExpectedOrderAndIcons()
    {
        var items = PrimaryNavigationMetadata.Items;

        Assert.Equal(4, items.Count);

        Assert.Collection(items,
            item =>
            {
                Assert.Equal(PrimaryNavDestination.Home, item.Destination);
                Assert.Equal("Home", item.Label);
                Assert.Equal("nav_home.svg", item.IconKey);
            },
            item =>
            {
                Assert.Equal(PrimaryNavDestination.Stream, item.Destination);
                Assert.Equal("Stream", item.Label);
                Assert.Equal("nav_stream.svg", item.IconKey);
            },
            item =>
            {
                Assert.Equal(PrimaryNavDestination.View, item.Destination);
                Assert.Equal("View", item.Label);
                Assert.Equal("nav_view.svg", item.IconKey);
            },
            item =>
            {
                Assert.Equal(PrimaryNavDestination.Settings, item.Destination);
                Assert.Equal("Settings", item.Label);
                Assert.Equal("nav_settings.svg", item.IconKey);
            });
    }

    [Fact]
    public void Items_HaveUniqueDestinations_AndUniqueRoutes()
    {
        var items = PrimaryNavigationMetadata.Items;

        Assert.Equal(items.Count, items.Select(i => i.Destination).Distinct().Count());
        Assert.Equal(items.Count, items.Select(i => i.Route).Distinct().Count());
        Assert.All(items, item => Assert.StartsWith("//", item.Route));
    }

    /// <summary>
    /// #395: one route family. These are the routes <c>AppShell.xaml</c>'s <c>PrimaryTabBar</c>
    /// declares (as <c>home</c>, <c>stream</c>, …) and <c>ShellNavigationService</c> navigates to,
    /// whichever chrome — bottom bar or rail — is showing. A <c>-tab</c>/<c>-rail</c> suffix
    /// coming back would mean a second family of ShellItems, which is what let a rotation re-point
    /// <c>Shell.CurrentItem</c> and destroy a pushed page (#393).
    /// </summary>
    [Fact]
    public void Items_Routes_AreExactlyTheOneRouteFamily()
    {
        Assert.Equal(
            new[] { "//home", "//stream", "//view", "//settings" },
            PrimaryNavigationMetadata.Items.Select(i => i.Route).ToArray());

        Assert.Equal(
            PrimaryNavigationMetadata.Items.Count,
            PrimaryNavigationMetadata.Items.Select(i => i.Route).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Theory]
    [InlineData("//home", PrimaryNavDestination.Home)]
    [InlineData("//stream", PrimaryNavDestination.Stream)]
    [InlineData("//view", PrimaryNavDestination.View)]
    [InlineData("//settings", PrimaryNavDestination.Settings)]
    [InlineData("//view/viewer?sourceId=x", PrimaryNavDestination.View)]
    [InlineData("//stream?resume=true", PrimaryNavDestination.Stream)]
    [InlineData("//stream?reStreamSourceId=HOST%20(view)&isReStreamMode=true", PrimaryNavDestination.Stream)]
    [InlineData("//HOME", PrimaryNavDestination.Home)]
    public void TryResolveDestination_ResolvesFromTheLastPathSegmentOnly(string location, PrimaryNavDestination expected)
    {
        Assert.Equal(expected, PrimaryNavigationMetadata.TryResolveDestination(location));
    }

    [Theory]
    [InlineData("//settings/diagnostic-log")]
    [InlineData("diagnostic-log")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("//")]
    [InlineData("?sourceId=view")]
    public void TryResolveDestination_ReturnsNull_WhenTheLastSegmentIsNotAPrimaryDestination(string? location)
    {
        Assert.Null(PrimaryNavigationMetadata.TryResolveDestination(location));
    }

    [Fact]
    public void TryResolveDestination_ResolvesEveryDeclaredRoute_ToItsOwnDestination()
    {
        Assert.All(PrimaryNavigationMetadata.Items, item =>
            Assert.Equal(item.Destination, PrimaryNavigationMetadata.TryResolveDestination(item.Route)));
    }

    /// <summary>
    /// The rail renders <see cref="PrimaryNavItem.IconGeometry"/> as a themeable vector (#294);
    /// a missing or malformed value would throw while the rail is being built.
    /// </summary>
    [Fact]
    public void Items_HaveDistinctIconGeometry_StartingWithAMoveCommand()
    {
        var items = PrimaryNavigationMetadata.Items;

        Assert.All(items, item =>
        {
            Assert.False(string.IsNullOrWhiteSpace(item.IconGeometry));
            Assert.StartsWith("M", item.IconGeometry, StringComparison.OrdinalIgnoreCase);
        });

        Assert.Equal(items.Count, items.Select(i => i.IconGeometry).Distinct().Count());
    }
}

public sealed class NavigationPolicyServiceTests
{
    private static (NavigationPolicyService Sut, WindowSizeClassService SizeClassService) CreateSut()
    {
        var sizeClassService = new WindowSizeClassService();
        return (new NavigationPolicyService(sizeClassService), sizeClassService);
    }

    // Placement matrix (#279): rail when landscape OR Expanded; bottom tabs otherwise.
    [Theory]
    [InlineData(DeviceOrientation.Portrait, WindowSizeClass.Compact, NavigationPlacementMode.Bottom)]
    [InlineData(DeviceOrientation.Portrait, WindowSizeClass.Medium, NavigationPlacementMode.Bottom)]
    [InlineData(DeviceOrientation.Portrait, WindowSizeClass.Expanded, NavigationPlacementMode.LeftRail)]
    [InlineData(DeviceOrientation.Landscape, WindowSizeClass.Compact, NavigationPlacementMode.LeftRail)]
    [InlineData(DeviceOrientation.Landscape, WindowSizeClass.Medium, NavigationPlacementMode.LeftRail)]
    [InlineData(DeviceOrientation.Landscape, WindowSizeClass.Expanded, NavigationPlacementMode.LeftRail)]
    public void ResolvePlacement_OrientationBySizeClassMatrix_ReturnsExpectedPlacement(
        DeviceOrientation orientation, WindowSizeClass sizeClass, NavigationPlacementMode expected)
    {
        var (sut, _) = CreateSut();

        var placement = sut.ResolvePlacement(orientation, sizeClass);

        Assert.Equal(expected, placement);
    }

    [Fact]
    public void UpdateOrientation_RaisesPlacementChanged_WhenPlacementActuallyChanges()
    {
        var (sut, _) = CreateSut();
        var raised = false;
        NavigationPlacementMode? raisedValue = null;

        sut.PlacementChanged += (_, value) =>
        {
            raised = true;
            raisedValue = value;
        };

        sut.UpdateOrientation(DeviceOrientation.Landscape);

        Assert.True(raised);
        Assert.Equal(NavigationPlacementMode.LeftRail, raisedValue);
        Assert.Equal(NavigationPlacementMode.LeftRail, sut.CurrentPlacement);
    }

    [Fact]
    public void UpdateOrientation_DoesNotRaisePlacementChanged_WhenPlacementStaysSame()
    {
        var (sut, _) = CreateSut();
        var eventCount = 0;

        sut.PlacementChanged += (_, _) => eventCount++;

        sut.UpdateOrientation(DeviceOrientation.Portrait);

        Assert.Equal(0, eventCount);
        Assert.Equal(NavigationPlacementMode.Bottom, sut.CurrentPlacement);
    }

    [Fact]
    public void SizeClassChangeToExpanded_InPortrait_SwitchesToLeftRail_WithoutOrientationChange()
    {
        var (sut, sizeClassService) = CreateSut();
        sut.UpdateOrientation(DeviceOrientation.Portrait);
        NavigationPlacementMode? raisedValue = null;
        sut.PlacementChanged += (_, value) => raisedValue = value;

        sizeClassService.UpdateFromWidth(900); // portrait 10" tablet → Expanded

        Assert.Equal(NavigationPlacementMode.LeftRail, raisedValue);
        Assert.Equal(NavigationPlacementMode.LeftRail, sut.CurrentPlacement);
    }

    [Fact]
    public void SizeClassChangeBackToCompact_InPortrait_ReturnsToBottomTabs()
    {
        var (sut, sizeClassService) = CreateSut();
        sut.UpdateOrientation(DeviceOrientation.Portrait);
        sizeClassService.UpdateFromWidth(900); // → LeftRail (Expanded)

        sizeClassService.UpdateFromWidth(400); // → Compact

        Assert.Equal(NavigationPlacementMode.Bottom, sut.CurrentPlacement);
    }

    [Fact]
    public void SizeClassChangeToExpanded_InLandscape_DoesNotRaiseRedundantPlacementChanged()
    {
        var (sut, sizeClassService) = CreateSut();
        sut.UpdateOrientation(DeviceOrientation.Landscape); // already LeftRail
        var eventCount = 0;
        sut.PlacementChanged += (_, _) => eventCount++;

        sizeClassService.UpdateFromWidth(900); // Expanded — placement is already LeftRail

        Assert.Equal(0, eventCount);
        Assert.Equal(NavigationPlacementMode.LeftRail, sut.CurrentPlacement);
    }

    [Fact]
    public void RotateToPortrait_OnExpandedWindow_KeepsLeftRail()
    {
        var (sut, sizeClassService) = CreateSut();
        sizeClassService.UpdateFromWidth(1000); // Expanded tablet
        sut.UpdateOrientation(DeviceOrientation.Landscape);

        sut.UpdateOrientation(DeviceOrientation.Portrait);

        Assert.Equal(NavigationPlacementMode.LeftRail, sut.CurrentPlacement);
    }
}

public sealed class AdaptiveShellStateViewModelTests
{
    [Fact]
    public void Constructor_UsesPolicyCurrentPlacement_AndExposesFourRouteMappings()
    {
        var policy = new FakeNavigationPolicyService(NavigationPlacementMode.Bottom);

        var sut = new AdaptiveShellStateViewModel(policy);

        Assert.Equal(NavigationPlacementMode.Bottom, sut.PlacementMode);
        Assert.True(sut.IsBottomNavigationVisible);
        Assert.False(sut.IsLeftRailNavigationVisible);
        Assert.Equal(4, sut.PrimaryItems.Count);
        Assert.Equal(4, sut.RouteByDestination.Count);
        Assert.Contains(PrimaryNavDestination.Home, sut.RouteByDestination.Keys);
        Assert.Contains(PrimaryNavDestination.Stream, sut.RouteByDestination.Keys);
        Assert.Contains(PrimaryNavDestination.View, sut.RouteByDestination.Keys);
        Assert.Contains(PrimaryNavDestination.Settings, sut.RouteByDestination.Keys);
    }

    [Fact]
    public void SelectDestination_UpdatesSelectedDestination_AndRaisesRailItemSelected()
    {
        var policy = new FakeNavigationPolicyService(NavigationPlacementMode.Bottom);
        var sut = new AdaptiveShellStateViewModel(policy);

        PrimaryNavDestination? raisedDestination = null;
        sut.RailItemSelected += (_, destination) => raisedDestination = destination;

        sut.SelectDestination(PrimaryNavDestination.Settings);

        Assert.Equal(PrimaryNavDestination.Settings, sut.SelectedDestination);
        Assert.Equal(PrimaryNavDestination.Settings, raisedDestination);
    }

    [Fact]
    public void PlacementChanged_FromPolicy_UpdatesViewModelPlacementAndVisibilityFlags()
    {
        var policy = new FakeNavigationPolicyService(NavigationPlacementMode.Bottom);
        var sut = new AdaptiveShellStateViewModel(policy);

        policy.EmitPlacementChanged(NavigationPlacementMode.LeftRail);

        Assert.Equal(NavigationPlacementMode.LeftRail, sut.PlacementMode);
        Assert.False(sut.IsBottomNavigationVisible);
        Assert.True(sut.IsLeftRailNavigationVisible);
    }

    // Locks the #384 decision, restated for #395: IsBottomNavigationVisible/IsLeftRailNavigationVisible
    // are pure PlacementMode queries. AppShell.ApplyPlacement writes the item-scope
    // Shell.TabBarIsVisible on PrimaryTabBar from them, and ViewerFullScreenChromeController's exit
    // ClearValue falls back to that value — so folding IsChromeSuppressed in would leave the bar
    // value stale after full screen. Suppression only hides the rail (NavigationChromePolicy). Do
    // not "simplify" these assertions to expect suppression to force the visibility flags false.
    [Fact]
    public void IsChromeSuppressed_SetAndCleared_DoesNotAffectPlacementModeOrDerivedVisibility()
    {
        var policy = new FakeNavigationPolicyService(NavigationPlacementMode.LeftRail);
        var sut = new AdaptiveShellStateViewModel(policy);

        sut.IsChromeSuppressed = true;

        Assert.True(sut.IsChromeSuppressed);
        Assert.Equal(NavigationPlacementMode.LeftRail, sut.PlacementMode);
        Assert.False(sut.IsBottomNavigationVisible);
        Assert.True(sut.IsLeftRailNavigationVisible);

        sut.IsChromeSuppressed = false;

        Assert.False(sut.IsChromeSuppressed);
        Assert.Equal(NavigationPlacementMode.LeftRail, sut.PlacementMode);
        Assert.True(sut.IsLeftRailNavigationVisible);
    }

    [Fact]
    public void IsChromeSuppressed_Set_RaisesPropertyChangedForItselfOnly()
    {
        var policy = new FakeNavigationPolicyService(NavigationPlacementMode.Bottom);
        var sut = new AdaptiveShellStateViewModel(policy);
        var raised = new List<string?>();
        sut.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        sut.IsChromeSuppressed = true;

        Assert.Contains(nameof(AdaptiveShellStateViewModel.IsChromeSuppressed), raised);
        Assert.DoesNotContain(nameof(AdaptiveShellStateViewModel.IsBottomNavigationVisible), raised);
        Assert.DoesNotContain(nameof(AdaptiveShellStateViewModel.IsLeftRailNavigationVisible), raised);
    }

    private sealed class FakeNavigationPolicyService : INavigationPolicyService
    {
        public FakeNavigationPolicyService(NavigationPlacementMode currentPlacement)
        {
            CurrentPlacement = currentPlacement;
        }

        public NavigationPlacementMode CurrentPlacement { get; private set; }

        public event EventHandler<NavigationPlacementMode>? PlacementChanged;

        public NavigationPlacementMode ResolvePlacement(DeviceOrientation orientation, WindowSizeClass sizeClass)
            => orientation == DeviceOrientation.Landscape || sizeClass == WindowSizeClass.Expanded
                ? NavigationPlacementMode.LeftRail
                : NavigationPlacementMode.Bottom;

        public void UpdateOrientation(DeviceOrientation orientation)
        {
            var next = ResolvePlacement(orientation, WindowSizeClass.Compact);
            if (next == CurrentPlacement)
                return;

            CurrentPlacement = next;
            PlacementChanged?.Invoke(this, CurrentPlacement);
        }

        public void EmitPlacementChanged(NavigationPlacementMode placement)
        {
            CurrentPlacement = placement;
            PlacementChanged?.Invoke(this, placement);
        }
    }
}

/// <summary>#395: the chrome AppShell.ApplyPlacement applies for a placement.</summary>
public sealed class NavigationChromePolicyTests
{
    [Theory]
    [InlineData(NavigationPlacementMode.Bottom, false, false, true)]
    [InlineData(NavigationPlacementMode.Bottom, true, false, true)]
    [InlineData(NavigationPlacementMode.LeftRail, false, true, false)]
    [InlineData(NavigationPlacementMode.LeftRail, true, false, false)]
    public void Resolve_MapsPlacementAndSuppression_ToRailAndItemScopeBar(
        NavigationPlacementMode placement,
        bool isChromeSuppressed,
        bool expectedRailVisible,
        bool expectedBottomBarVisibleAtItemScope)
    {
        var (railVisible, bottomBarVisibleAtItemScope) = NavigationChromePolicy.Resolve(placement, isChromeSuppressed);

        Assert.Equal(expectedRailVisible, railVisible);
        Assert.Equal(expectedBottomBarVisibleAtItemScope, bottomBarVisibleAtItemScope);
    }

    /// <summary>
    /// Suppression is page-scoped for the bar (ViewerFullScreenChromeController); the item-scope
    /// value follows the placement only, so the controller's ClearValue on exit restores the right
    /// bar. A suppression-dependent item-scope value would be a second writer of the bar.
    /// </summary>
    [Theory]
    [InlineData(NavigationPlacementMode.Bottom)]
    [InlineData(NavigationPlacementMode.LeftRail)]
    public void Resolve_Suppression_NeverChangesTheItemScopeBarValue(NavigationPlacementMode placement)
    {
        Assert.Equal(
            NavigationChromePolicy.Resolve(placement, isChromeSuppressed: false).BottomBarVisibleAtItemScope,
            NavigationChromePolicy.Resolve(placement, isChromeSuppressed: true).BottomBarVisibleAtItemScope);
    }

    [Theory]
    [InlineData(NavigationPlacementMode.Bottom)]
    [InlineData(NavigationPlacementMode.LeftRail)]
    public void Resolve_NeverShowsBothRailAndBottomBar(NavigationPlacementMode placement)
    {
        foreach (var suppressed in new[] { false, true })
        {
            var chrome = NavigationChromePolicy.Resolve(placement, suppressed);
            Assert.False(chrome.RailVisible && chrome.BottomBarVisibleAtItemScope);
        }
    }
}
