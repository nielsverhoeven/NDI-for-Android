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
