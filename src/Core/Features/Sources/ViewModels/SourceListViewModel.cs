using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiForAndroid.Features.Settings.Services;
using NdiForAndroid.Features.Sources.Models;
using NdiForAndroid.Features.Sources.Repositories;
using NdiForAndroid.NdiBridge;
using NdiForAndroid.Services;

namespace NdiForAndroid.Features.Sources.ViewModels;

public partial class SourceListViewModel : ObservableObject
{
    private readonly ISourceRepository _repository;
    private readonly INavigationService _navigation;
    private readonly IDiscoverySettingsOrchestrator _orchestrator;
    private readonly IDiscoveryRefreshService _refreshService;

    [ObservableProperty]
    private IReadOnlyList<NdiSource> _sources = Array.Empty<NdiSource>();

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _activeDiscoveryModeLabel = "mDNS";

    public SourceListViewModel(
        ISourceRepository repository,
        INavigationService navigation,
        IDiscoverySettingsOrchestrator orchestrator,
        IDiscoveryRefreshService refreshService)
    {
        _repository     = repository;
        _navigation     = navigation;
        _orchestrator   = orchestrator;
        _refreshService = refreshService;

        _refreshService.SnapshotReady += OnSnapshotReady;
    }

    private void OnSnapshotReady(object? sender, DiscoverySnapshot snapshot)
    {
        // MAUI's binding infrastructure dispatches ObservableProperty change notifications
        // to the UI thread automatically; no explicit MainThread.Invoke needed in Core layer.
        Sources      = snapshot.Sources;
        ErrorMessage = snapshot.Status == DiscoveryStatus.Failure ? snapshot.ErrorMessage : null;
        IsRefreshing = false;
        UpdateDiscoveryModeLabel();
    }

    [RelayCommand]
    private Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsRefreshing = true;
        ErrorMessage = null;
        // Delegate to the service; result arrives asynchronously via SnapshotReady
        _refreshService.RequestRefresh();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void StopDiscovery()
    {
        _refreshService.Stop();
    }

    [RelayCommand]
    private Task NavigateToViewerAsync(NdiSource source) =>
        _navigation.NavigateToAsync($"viewer?sourceId={Uri.EscapeDataString(source.SourceId)}");

    private void UpdateDiscoveryModeLabel()
    {
        ActiveDiscoveryModeLabel = _orchestrator.ActiveMode switch
        {
            DiscoveryMode.DiscoveryServer => BuildDiscoveryServerLabel(),
            _ => "mDNS",
        };
    }

    private string BuildDiscoveryServerLabel()
    {
        // The label shows the primary (first) active endpoint for user feedback.
        return "Discovery Server";
    }
}
