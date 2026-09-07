using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiForAndroid.Features.AppState.Models;
using NdiForAndroid.Features.AppState.Repositories;
using NdiForAndroid.Features.Navigation.Models;
using NdiForAndroid.Features.Sources.Models;
using NdiForAndroid.Features.Sources.Repositories;
using NdiForAndroid.NdiBridge;
using NdiForAndroid.Services;

namespace NdiForAndroid.Features.Home.ViewModels;

/// <summary>Home dashboard: discovery / viewer / output status summary and the quick actions.</summary>
/// <remarks>
/// Registered as a DI <b>Singleton</b> together with <c>HomePage</c> (#352/#359): it subscribes here to
/// singleton events, while MAUI's Android Shell re-resolves the tab-root page on every tab entry and on
/// every -tab/-rail placement change. A Transient lifetime would leak one subscribed instance per visit.
/// Per-visit work belongs in <see cref="RefreshCommand"/> (run by <c>HomePage.OnAppearing</c>), never in
/// the constructor. <see cref="Dispose"/> is container-owned (app teardown) — pages must not call it.
/// </remarks>
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

    [ObservableProperty]
    private string? _lastViewerSourceId;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartViewingLastSourceCommand))]
    private bool _hasLastViewerSource;

    [ObservableProperty]
    private string? _lastOutputStreamName;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ResumeOutputCommand))]
    private bool _canResumeOutput;

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

            var outputActive = state.IsOutputActive && _outputBridge.IsActive;
            OutputStatus = outputActive
                ? $"Active output to \"{state.StreamName ?? "unknown"}\""
                : "Idle (no active output)";

            LastViewerSourceId = state.LastViewerSourceId;
            HasLastViewerSource = !string.IsNullOrWhiteSpace(state.LastViewerSourceId);

            LastOutputStreamName = state.StreamName;
            CanResumeOutput = !outputActive && !string.IsNullOrWhiteSpace(state.StreamName);

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

    [RelayCommand(CanExecute = nameof(HasLastViewerSource))]
    private async Task StartViewingLastSourceAsync()
    {
        if (string.IsNullOrWhiteSpace(LastViewerSourceId))
            return;

        await _navigationService.NavigateToPrimaryAsync(PrimaryNavDestination.View);
        await _navigationService.NavigateToAsync($"viewer?sourceId={Uri.EscapeDataString(LastViewerSourceId)}");
    }

    [RelayCommand(CanExecute = nameof(CanResumeOutput))]
    private async Task ResumeOutputAsync()
    {
        if (!CanResumeOutput)
            return;

        await _navigationService.NavigateToPrimaryAsync(PrimaryNavDestination.Stream, "resume=true");
    }

    /// <summary>Container-owned teardown only (singleton lifetime) — never called from page lifecycle.</summary>
    public void Dispose()
    {
        _discoveryService.SnapshotReady -= OnDiscoverySnapshot;
        _outputBridge.OutputStatusChanged -= OnOutputStatusChanged;
    }
}
