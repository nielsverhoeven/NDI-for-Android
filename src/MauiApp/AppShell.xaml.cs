using NdiForAndroid.Features.Navigation.Models;
using NdiForAndroid.Features.Navigation.Services;
using NdiForAndroid.Features.Navigation.ViewModels;
using NdiForAndroid.Features.Output.Views;
using NdiForAndroid.Features.Viewer.Views;
using NdiForAndroid.Services;

namespace NdiForAndroid;

public partial class AppShell : Shell
{
    private readonly AdaptiveShellStateViewModel _stateViewModel;
    private readonly IAndroidOrientationBridge _orientationBridge;
    private readonly INavigationHandoffService _handoffService;

    private PrimaryNavDestination _currentPrimaryDestination = PrimaryNavDestination.Home;

    public AppShell(
        AdaptiveShellStateViewModel stateViewModel,
        IAndroidOrientationBridge orientationBridge,
        INavigationHandoffService handoffService)
    {
        InitializeComponent();

        _stateViewModel = stateViewModel;
        _orientationBridge = orientationBridge;
        _handoffService = handoffService;

        BindingContext = _stateViewModel;

        Routing.RegisterRoute("viewer", typeof(ViewerPage));
        Routing.RegisterRoute("output", typeof(OutputPage));

        _stateViewModel.PropertyChanged += OnStateViewModelPropertyChanged;
        Navigated += OnShellNavigated;

        _orientationBridge.SyncFromDisplayInfo();
        ApplyFlyoutBehavior();
    }

    private void OnStateViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AdaptiveShellStateViewModel.PlacementMode))
            ApplyFlyoutBehavior();
    }

    private void ApplyFlyoutBehavior()
    {
        FlyoutBehavior = _stateViewModel.IsLeftRailNavigationVisible
            ? FlyoutBehavior.Locked
            : FlyoutBehavior.Disabled;
    }

    private async void OnShellNavigated(object? sender, ShellNavigatedEventArgs e)
    {
        var from = TryParseDestination(e.Previous.Location.OriginalString) ?? _currentPrimaryDestination;
        var to = TryParseDestination(e.Current.Location.OriginalString) ?? _currentPrimaryDestination;

        if (from != to)
            await _handoffService.HandlePrimaryDestinationChangeAsync(from, to);

        _currentPrimaryDestination = to;
    }

    private static PrimaryNavDestination? TryParseDestination(string? location)
    {
        if (string.IsNullOrWhiteSpace(location))
            return null;

        var normalized = location.ToLowerInvariant();

        if (normalized.Contains("home") || normalized.Contains("sources"))
            return PrimaryNavDestination.Home;

        if (normalized.Contains("stream") || normalized.Contains("output"))
            return PrimaryNavDestination.Stream;

        if (normalized.Contains("view") || normalized.Contains("viewer"))
            return PrimaryNavDestination.View;

        if (normalized.Contains("settings"))
            return PrimaryNavDestination.Settings;

        return null;
    }
}
