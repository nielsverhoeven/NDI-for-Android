using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiForAndroid.Features.AppState.Models;
using NdiForAndroid.Features.AppState.Repositories;
using NdiForAndroid.Features.Navigation.Services;
using NdiForAndroid.Features.Settings.Services;
using NdiForAndroid.Features.Sources.Models;
using NdiForAndroid.Features.Sources.Repositories;
using NdiForAndroid.Features.Viewer.ViewModels;
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
    private readonly IWindowSizeClassService _windowSizeClassService;
    private readonly Func<ViewerViewModel> _viewerViewModelFactory;

    [ObservableProperty]
    private IReadOnlyList<NdiSource> _sources = Array.Empty<NdiSource>();

    [ObservableProperty]
    private bool _isRefreshing;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _activeDiscoveryModeLabel = "mDNS";

    /// <summary>
    /// ViewModel for the embedded viewer pane shown next to the list on Expanded
    /// windows. Lazily created on first Expanded selection (this VM is a Singleton;
    /// ViewerViewModel is Transient — resolved via the injected factory). Null until
    /// the first Expanded tap, so phones never pay for it.
    /// </summary>
    [ObservableProperty]
    private ViewerViewModel? _paneViewer;

    public SourceListViewModel(
        ISourceRepository repository,
        INavigationService navigation,
        IDiscoverySettingsOrchestrator orchestrator,
        IDiscoveryRefreshService refreshService,
        IAppStateRepository appStateRepo,
        IWindowSizeClassService windowSizeClassService,
        Func<ViewerViewModel> viewerViewModelFactory)
    {
        _repository     = repository;
        _navigation     = navigation;
        _orchestrator   = orchestrator;
        _refreshService = refreshService;
        _appStateRepo   = appStateRepo;
        _windowSizeClassService = windowSizeClassService;
        _viewerViewModelFactory = viewerViewModelFactory;

        _refreshService.SnapshotReady += OnSnapshotReady;
        _windowSizeClassService.Changed += OnWindowSizeClassChanged;
    }

    private void OnWindowSizeClassChanged(object? sender, WindowSizeClass sizeClass)
    {
        // Leaving Expanded collapses the pane: stop its playback so the single
        // shared INdiViewerBridge cannot be double-driven by a later pushed
        // ViewerPage. Entering Expanded never auto-plays — a source tap is required.
        if (sizeClass != WindowSizeClass.Expanded && PaneViewer is { IsPlaying: true } pane)
            pane.StopCommand.Execute(null);
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

        if (_windowSizeClassService.Current == WindowSizeClass.Expanded)
        {
            // Two-pane mode: play in the embedded pane — no navigation push,
            // so the list stays visible and the back stack is untouched.
            PaneViewer ??= _viewerViewModelFactory();
            PaneViewer.SourceId = source.SourceId; // setting SourceId starts playback
            return;
        }

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
