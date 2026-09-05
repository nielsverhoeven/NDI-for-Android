using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiForAndroid.Features.AppState.Models;
using NdiForAndroid.Features.AppState.Repositories;
using NdiForAndroid.Features.Sources.Models;
using NdiForAndroid.Features.Sources.Repositories;
using NdiForAndroid.NdiBridge;
using NdiForAndroid.Services;

namespace NdiForAndroid.Features.Home.ViewModels;

public partial class HomeViewModel : ObservableObject, IDisposable
{
    private readonly IDiscoveryRefreshService _discoveryService;
    private readonly ISourceRepository _sourceRepository;
    private readonly IAppStateRepository _appStateRepo;
    private readonly INavigationService _navigationService;
    private readonly INdiOutputBridge _outputBridge;
    private readonly IMainThreadDispatcher _dispatcher;

    [ObservableProperty]
    private string? _discoveryStatus;

    [ObservableProperty]
    private int _sourceCount;

    [ObservableProperty]
    private string? _lastRefreshDisplay;

    [ObservableProperty]
    private string? _viewerStatus;

    [ObservableProperty]
    private string? _outputStatus;

    public HomeViewModel(
        IDiscoveryRefreshService discoveryService,
        ISourceRepository sourceRepository,
        IAppStateRepository appStateRepo,
        INavigationService navigationService,
        INdiOutputBridge outputBridge,
        IMainThreadDispatcher dispatcher)
    {
        _discoveryService = discoveryService;
        _sourceRepository = sourceRepository;
        _appStateRepo = appStateRepo;
        _navigationService = navigationService;
        _outputBridge = outputBridge;
        _dispatcher = dispatcher;

        DiscoveryStatus = "Waiting for discovery...";
        SourceCount = 0;
        LastRefreshDisplay = null;
        ViewerStatus = "Idle (no source viewed yet)";
        OutputStatus = "Idle (no active output)";

        // Subscribe to discovery snapshots
        _discoveryService.SnapshotReady += OnDiscoverySnapshot;
        _outputBridge.OutputStatusChanged += OnOutputStatusChanged;

        RefreshCommand.Execute(null);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var state = await _appStateRepo.RestoreStateAsync();
        var cachedSources = await _sourceRepository.GetCachedSourcesAsync();

        _dispatcher.BeginInvokeOnMainThread(() =>
        {
            ViewerStatus = string.IsNullOrWhiteSpace(state.LastViewerSourceId)
                ? "Idle (no source viewed yet)"
                : $"Last viewed: {state.LastViewerSourceId}";

            OutputStatus = state.IsOutputActive && _outputBridge.IsActive
                ? $"Active output to \"{state.StreamName ?? "unknown"}\""
                : "Idle (no active output)";

            if (cachedSources.Count > 0)
            {
                SourceCount = cachedSources.Count;
                DiscoveryStatus = "Connected to NDI network";
            }
        });
    }

    private void OnDiscoverySnapshot(object? sender, DiscoverySnapshot snapshot)
    {
        var refreshTime = DateTimeOffset.FromUnixTimeMilliseconds(snapshot.CompletedAtEpochMillis).LocalDateTime;

        _dispatcher.BeginInvokeOnMainThread(() =>
        {
            var status = snapshot.Status;
            DiscoveryStatus = status switch
            {
                Features.Sources.Models.DiscoveryStatus.Success => "Connected to NDI network",
                Features.Sources.Models.DiscoveryStatus.Empty => "No sources found",
                Features.Sources.Models.DiscoveryStatus.Failure => snapshot.ErrorMessage ?? "Discovery failed",
                _ => "Discovering..."
            };

            SourceCount = snapshot.Sources.Count;
            LastRefreshDisplay = $"Last refresh: {refreshTime:HH:mm:ss}";
        });
    }

    private void OnOutputStatusChanged(object? sender, EventArgs e) =>
        _dispatcher.BeginInvokeOnMainThread(() => _ = RefreshCommand.ExecuteAsync(null));

    [RelayCommand]
    private async Task StartViewingLastSource()
    {
        var state = await _appStateRepo.RestoreStateAsync();
        
        if (!string.IsNullOrWhiteSpace(state.LastViewerSourceId))
        {
            // Navigate to View tab with the last source
            await _navigationService.NavigateToAsync($"view-tab?sourceId={state.LastViewerSourceId}");
        }
    }

    [RelayCommand]
    private async Task ResumeOutput()
    {
        var state = await _appStateRepo.RestoreStateAsync();
        
        if (state.IsOutputActive && !string.IsNullOrWhiteSpace(state.StreamName))
        {
            // Navigate to Stream tab to resume output
            await _navigationService.NavigateToAsync($"stream-tab?streamName={state.StreamName}");
        }
    }

    public void Dispose()
    {
        _discoveryService.SnapshotReady -= OnDiscoverySnapshot;
        _outputBridge.OutputStatusChanged -= OnOutputStatusChanged;
    }
}
