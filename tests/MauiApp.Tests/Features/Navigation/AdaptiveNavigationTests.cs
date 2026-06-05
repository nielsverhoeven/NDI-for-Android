using NdiForAndroid.Features.Navigation.Models;
using NdiForAndroid.Features.Navigation.Services;
using NdiForAndroid.Features.Navigation.ViewModels;
using Xunit;

namespace MauiApp.Tests.Features.Navigation;

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
    [Fact]
    public void ResolvePlacement_ReturnsBottom_ForPortrait()
    {
        var sut = new NavigationPolicyService();

        var placement = sut.ResolvePlacement(DeviceOrientation.Portrait);

        Assert.Equal(NavigationPlacementMode.Bottom, placement);
    }

    [Fact]
    public void ResolvePlacement_ReturnsLeftRail_ForLandscape()
    {
        var sut = new NavigationPolicyService();

        var placement = sut.ResolvePlacement(DeviceOrientation.Landscape);

        Assert.Equal(NavigationPlacementMode.LeftRail, placement);
    }

    [Fact]
    public void UpdateOrientation_RaisesPlacementChanged_WhenPlacementActuallyChanges()
    {
        var sut = new NavigationPolicyService();
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
        var sut = new NavigationPolicyService();
        var eventCount = 0;

        sut.PlacementChanged += (_, _) => eventCount++;

        sut.UpdateOrientation(DeviceOrientation.Portrait);

        Assert.Equal(0, eventCount);
        Assert.Equal(NavigationPlacementMode.Bottom, sut.CurrentPlacement);
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

        public NavigationPlacementMode ResolvePlacement(DeviceOrientation orientation)
            => orientation == DeviceOrientation.Landscape
                ? NavigationPlacementMode.LeftRail
                : NavigationPlacementMode.Bottom;

        public void UpdateOrientation(DeviceOrientation orientation)
        {
            var next = ResolvePlacement(orientation);
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
