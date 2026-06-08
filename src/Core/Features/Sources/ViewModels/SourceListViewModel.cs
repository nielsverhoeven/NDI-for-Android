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

    private CancellationTokenSource? _periodicRefreshCts;

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
        IDiscoverySettingsOrchestrator orchestrator)
    {
        _repository = repository;
        _navigation = navigation;
        _orchestrator = orchestrator;
    }

    [RelayCommand]
    private async Task RefreshAsync(CancellationToken cancellationToken)
    {
        IsRefreshing = true;
        ErrorMessage = null;
        try
        {
            var snapshot = await _repository.DiscoverAsync(cancellationToken);
            Sources = snapshot.Sources;
            if (snapshot.Status == DiscoveryStatus.Failure)
                ErrorMessage = snapshot.ErrorMessage;

            UpdateDiscoveryModeLabel();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private void StopDiscovery()
    {
        _periodicRefreshCts?.Cancel();
        _periodicRefreshCts?.Dispose();
        _periodicRefreshCts = null;
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
        // The orchestrator manages the full endpoint list; we surface only the primary one here.
        return "Discovery Server";
    }
}
