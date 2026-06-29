using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiForAndroid.Features.AppState.Models;
using NdiForAndroid.Features.AppState.Repositories;
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
    private readonly IAppStateRepository _appStateRepo;

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
        IDiscoveryRefreshService refreshService,
        IAppStateRepository appStateRepo)
    {
        _repository     = repository;
        _navigation     = navigation;
        _orchestrator   = orchestrator;
        _refreshService = refreshService;
        _appStateRepo   = appStateRepo;

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
    private async Task NavigateToViewerAsync(NdiSource source)
    {
        // Persist last selected source for resume recovery
        var snapshot = await _appStateRepo.RestoreStateAsync();
        await _appStateRepo.SaveAsync(new AppStateSnapshot(
            snapshot.LastViewerSourceId,
            snapshot.StreamName,
            snapshot.IsOutputActive,
            source.SourceId));
        // Navigate (fire and forget in ViewModel — Shell handles result)
        try { await _navigation.NavigateToAsync($"viewer?sourceId={Uri.EscapeDataString(source.SourceId)}"); } catch { /* Navigation failures are handled by Shell */ }
    }

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
