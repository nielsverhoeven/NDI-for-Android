using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiForAndroid.Features.AppState.Models;
using NdiForAndroid.Features.AppState.Repositories;
using NdiForAndroid.Features.Output.Repositories;
using NdiForAndroid.Features.Sources.Models;
using NdiForAndroid.Features.Sources.Repositories;
using NdiForAndroid.NdiBridge;
using NdiForAndroid.Services;

namespace NdiForAndroid.Features.Output.ViewModels;

/// <summary>Stream tab: outgoing NDI output (capture or re-stream) configuration and start/stop.</summary>
/// <remarks>
/// Registered as a DI <b>Singleton</b> together with <c>OutputPage</c> (#352/#359): it subscribes here to the
/// singleton bridge's <c>OutputStatusChanged</c> and to <c>IAppLifecycleService.AppResumed</c>, while MAUI's
/// Android Shell re-resolves the tab-root page on every tab entry and placement change. A Transient lifetime
/// leaked one subscribed instance per visit, each still running <see cref="CorroborateWithBridgeAsync"/> on
/// resume. Consequently the observable state persists across tab visits; truthfulness comes from
/// <see cref="LoadCommand"/>, which <c>OutputPage.OnAppearing</c> awaits on every appearance and which
/// corroborates against the live bridge. <see cref="Dispose"/> is container-owned — pages must not call it.
/// </remarks>
public partial class OutputViewModel : ObservableObject, IDisposable
{
    private readonly INdiOutputBridge _bridge;
    private readonly IAppStateRepository _appStateRepo;
    private readonly IAppLifecycleService _lifecycle;
    private readonly IOutputConfigurationRepository _configRepo;
    private readonly IMainThreadDispatcher _dispatcher;
    private readonly IScreenReaderAnnouncer _announcer;
    private readonly ISourceRepository _sourceRepository;
    private readonly IDiscoveryRefreshService _discoveryService;

    /// <summary>
    /// Gates screen-reader announcements from <see cref="SetStatus"/> so the constructor's
    /// initial message is not spoken — the page is not on screen yet. Set true as the last
    /// statement of the constructor.
    /// </summary>
    private readonly bool _announceStatusChanges;

    [ObservableProperty]
    private string _streamName = "NDI-Android";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ToggleMicrophoneCommand))]
    private bool _isOutputActive;

    [ObservableProperty]
    private string? _statusMessage;

    /// <summary>
    /// True while <see cref="StatusMessage"/> describes a failure (validation, declined
    /// permission, start exception) rather than routine or positive information. OutputPage
    /// binds ErrorText, Bold and the warning glyph to it — the text alone never distinguishes
    /// the two message classes (#349, Nielsen #9).
    /// </summary>
    [ObservableProperty]
    private bool _isStatusError;

    /// <summary>Video input feeding the output: device screen or front/rear camera.</summary>
    [ObservableProperty]
    private VideoInputKind _selectedInputKind = VideoInputKind.Screen;

    /// <summary>Also capture and send the device microphone audio.</summary>
    [ObservableProperty]
    private bool _captureMicrophone;

    /// <summary>True while any connected receiver reports this sender on program tally.</summary>
    [ObservableProperty]
    private bool _isOnProgramTally;

    /// <summary>Number of receivers currently connected to this sender.</summary>
    [ObservableProperty]
    private int _connectionCount;

    /// <summary>
    /// When true, the output will capture and re-stream a discovered NDI source
    /// rather than broadcasting the selected local capture input.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowReStreamSourcePicker))]
    [NotifyPropertyChangedFor(nameof(ShowReStreamManualEntry))]
    private bool _isReStreamMode;

    /// <summary>
    /// The SourceId of the discovered NDI source to re-stream (set when switching
    /// into re-stream mode).
    /// </summary>
    [ObservableProperty]
    private string? _reStreamSourceId;

    /// <summary>
    /// Live-discovered + cached NDI sources offered by the re-stream source Picker (#343 OUT-08).
    /// Populated from <see cref="ISourceRepository.GetCachedSourcesAsync"/> on every
    /// <see cref="LoadAsync"/> and kept current via <see cref="IDiscoveryRefreshService.SnapshotReady"/>.
    /// </summary>
    public ObservableCollection<NdiSource> AvailableReStreamSources { get; } = new();

    /// <summary>True once <see cref="AvailableReStreamSources"/> has a real (non-placeholder) entry.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowReStreamSourcePicker))]
    [NotifyPropertyChangedFor(nameof(ShowReStreamManualEntry))]
    private bool _hasReStreamSources;

    /// <summary>
    /// The re-stream Picker's current selection. Setting it updates <see cref="ReStreamSourceId"/>
    /// so every existing consumer of the raw id (bridge calls, stream-name derivation) is unaffected.
    /// </summary>
    [ObservableProperty]
    private NdiSource? _selectedReStreamSource;

    /// <summary>True while re-stream mode is active and at least one source can be picked from.</summary>
    public bool ShowReStreamSourcePicker => IsReStreamMode && HasReStreamSources;

    /// <summary>
    /// True while re-stream mode is active and no source is cached/discovered yet — falls back to
    /// the free-text Entry rather than showing an empty, unusable Picker.
    /// </summary>
    public bool ShowReStreamManualEntry => IsReStreamMode && !HasReStreamSources;

    public IReadOnlyList<VideoInputKind> AvailableInputKinds { get; } = new[]
    {
        VideoInputKind.Screen,
        VideoInputKind.CameraFront,
        VideoInputKind.CameraRear,
    };

    public OutputViewModel(
        INdiOutputBridge bridge,
        IAppStateRepository appStateRepo,
        IAppLifecycleService lifecycle,
        IOutputConfigurationRepository configRepo,
        IMainThreadDispatcher dispatcher,
        IScreenReaderAnnouncer announcer,
        ISourceRepository sourceRepository,
        IDiscoveryRefreshService discoveryService)
    {
        _bridge = bridge;
        _appStateRepo = appStateRepo;
        _lifecycle = lifecycle;
        _configRepo = configRepo;
        _dispatcher = dispatcher;
        _announcer = announcer;
        _sourceRepository = sourceRepository;
        _discoveryService = discoveryService;
        SetStatus("Tap Start to begin broadcasting from this device.");

        _lifecycle.AppResumed += OnAppResumed;
        _bridge.OutputStatusChanged += OnOutputStatusChanged;
        _discoveryService.SnapshotReady += OnReStreamSourcesSnapshotReady;

        // The constructor's message is spoken to nobody — the page is not on screen yet (#345).
        _announceStatusChanges = true;
    }

    /// <summary>
    /// The only writer of <see cref="StatusMessage"/> and <see cref="IsStatusError"/>, so the two
    /// cannot drift apart, and the single point every status transition is spoken through the
    /// screen reader (WCAG 4.1.3, #345 OUT-03) — the status label is not the focused element when
    /// the message changes, so TalkBack would otherwise say nothing. Error statuses additionally
    /// drive the ErrorText/Bold/glyph treatment on OutputPage (#349, Nielsen #9). Null/blank
    /// clears are never announced.
    /// </summary>
    private void SetStatus(string? message, bool isError = false)
    {
        var changed = !string.Equals(StatusMessage, message, StringComparison.Ordinal);
        StatusMessage = message;
        IsStatusError = isError && !string.IsNullOrEmpty(message);

        if (_announceStatusChanges && changed && !string.IsNullOrWhiteSpace(message))
            _announcer.Announce(message);
    }

    /// <summary>Loads the persisted output configuration (called when the page appears).</summary>
    [RelayCommand]
    private async Task LoadAsync()
    {
        var config = await _configRepo.GetAsync();
        if (config is not null)
        {
            if (!string.IsNullOrWhiteSpace(config.PreferredStreamName))
                StreamName = config.PreferredStreamName;

            SelectedInputKind = config.InputKind;
            CaptureMicrophone = config.CaptureMicrophone;
        }

        await RefreshReStreamSourcesAsync();
        await CorroborateWithBridgeAsync("Output active");
    }

    /// <summary>
    /// Populates <see cref="AvailableReStreamSources"/> from the cached source registry (#343
    /// OUT-08) — the same registry <see cref="HomeViewModel"/> reads for its "Last viewed" friendly
    /// name. Live discovery updates arrive separately via <see cref="OnReStreamSourcesSnapshotReady"/>.
    /// </summary>
    private async Task RefreshReStreamSourcesAsync()
    {
        var cached = await _sourceRepository.GetCachedSourcesAsync();
        ApplyReStreamSources(cached);
    }

    /// <summary>Raised on a background/pump thread whenever a discovery poll completes — marshal to
    /// the UI thread before touching <see cref="AvailableReStreamSources"/> (Architecture Rule 4).
    /// A failed poll carries an empty source list (DiscoveryRefreshService raises a Failure snapshot
    /// with Array.Empty) and must never be allowed to empty the picker.</summary>
    private void OnReStreamSourcesSnapshotReady(object? sender, DiscoverySnapshot snapshot)
    {
        if (snapshot.Status == DiscoveryStatus.Failure)
            return;

        _dispatcher.BeginInvokeOnMainThread(() => ApplyReStreamSources(snapshot.Sources));
    }

    /// <summary>
    /// Merges <paramref name="sources"/> into <see cref="AvailableReStreamSources"/> by SourceId.
    /// Deliberately additive: discovery polls every 5 seconds, and clearing a Picker's bound
    /// ItemsSource resets its SelectedIndex to -1 — which would null SelectedReStreamSource (and
    /// with it ReStreamSourceId) on every poll. A source that did not answer one poll (weak Wi-Fi,
    /// source briefly busy) also stays selectable, which is the whole reason this list is a union
    /// of the cached registry and live discovery rather than the latest snapshot.
    /// </summary>
    private void ApplyReStreamSources(IReadOnlyList<NdiSource> sources)
    {
        foreach (var source in sources)
        {
            var index = IndexOfReStreamSource(source.SourceId);
            if (index < 0)
            {
                AvailableReStreamSources.Add(source);
            }
            else if (AvailableReStreamSources[index].LastSeenAtEpochMillis == 0)
            {
                // A synthesized raw-id placeholder (LastSeenAtEpochMillis == 0 is only ever set by
                // SelectReStreamSourceById) has now been discovered for real — swap in the real
                // entry so the Picker shows its friendly name instead of the raw id.
                AvailableReStreamSources[index] = source;
                if (string.Equals(SelectedReStreamSource?.SourceId, source.SourceId, StringComparison.Ordinal))
                    SelectedReStreamSource = source;
            }
        }

        HasReStreamSources = AvailableReStreamSources.Any(s => s.LastSeenAtEpochMillis != 0);

        // A restored/preselected id with no Picker selection yet (first LoadAsync) — point the
        // selection at it now that the list is populated.
        if (SelectedReStreamSource is null && !string.IsNullOrWhiteSpace(ReStreamSourceId))
            SelectReStreamSourceById(ReStreamSourceId);
    }

    private int IndexOfReStreamSource(string sourceId)
    {
        for (var i = 0; i < AvailableReStreamSources.Count; i++)
        {
            if (string.Equals(AvailableReStreamSources[i].SourceId, sourceId, StringComparison.Ordinal))
                return i;
        }

        return -1;
    }

    /// <summary>
    /// Points the Picker at the entry matching <paramref name="sourceId"/>. When the picker is the
    /// visible control but the id is not in it yet — e.g. a source preselected from the Sources
    /// page's Output button that this view model's cache/snapshot has not reconciled — a
    /// placeholder showing the raw id is synthesized and selected, so the Picker is never blank for
    /// a source the user just chose (Nielsen #6). With an empty registry the free-text Entry is the
    /// visible control instead, so only ReStreamSourceId is set: synthesizing there would flip
    /// HasReStreamSources and permanently hide the manual-entry fallback on this Singleton VM.
    /// </summary>
    private void SelectReStreamSourceById(string? sourceId)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
        {
            SelectedReStreamSource = null;
            ReStreamSourceId = null;
            return;
        }

        var index = IndexOfReStreamSource(sourceId);
        if (index >= 0)
        {
            SelectedReStreamSource = AvailableReStreamSources[index];
            return;
        }

        if (HasReStreamSources)
        {
            var placeholder = new NdiSource(sourceId, sourceId, null, IsAvailable: false, LastSeenAtEpochMillis: 0);
            AvailableReStreamSources.Insert(0, placeholder);
            SelectedReStreamSource = placeholder;
            return;
        }

        // Empty registry: the manual-entry Entry is what is on screen — set the id it binds to.
        SelectedReStreamSource = null;
        ReStreamSourceId = sourceId;
    }

    partial void OnSelectedReStreamSourceChanged(NdiSource? value)
    {
        ReStreamSourceId = value?.SourceId;
    }

    private async void OnAppResumed()
    {
        await CorroborateWithBridgeAsync("Output session restored.");
    }

    /// <summary>
    /// Reconciles observable state against the persisted snapshot and the live bridge — only
    /// claim an active/restored session when the bridge corroborates it. A stale persisted flag
    /// (process death, revoked permission, camera/mic loss while backgrounded) must not lie to
    /// the user.
    /// </summary>
    private async Task CorroborateWithBridgeAsync(string activeStatusMessage)
    {
        var state = await _appStateRepo.RestoreStateAsync();
        if (string.IsNullOrWhiteSpace(state.StreamName))
            return;

        if (_bridge.IsActive)
        {
            _dispatcher.BeginInvokeOnMainThread(() =>
            {
                IsOutputActive = true;
                StreamName = state.StreamName;
                IsReStreamMode = _bridge.IsReStreamActive;
                ConnectionCount = _bridge.ConnectionCount;
                IsOnProgramTally = _bridge.IsOnProgramTally;
                SetStatus(activeStatusMessage);
            });
        }
        else
        {
            if (state.IsOutputActive)
            {
                await _appStateRepo.SaveAsync(new AppStateSnapshot(
                    state.LastViewerSourceId, state.StreamName, false, state.LastSelectedSourceId));
            }

            _dispatcher.BeginInvokeOnMainThread(() =>
            {
                IsOutputActive = false;
                StreamName = state.StreamName;
                SetStatus("Tap Start to resume output");
            });
        }
    }

    /// <summary>Bridge status poll changed — marshal tally/connection refresh to the UI thread.</summary>
    private void OnOutputStatusChanged(object? sender, EventArgs e)
    {
        _dispatcher.BeginInvokeOnMainThread(() =>
        {
            IsOnProgramTally = _bridge.IsOnProgramTally;
            ConnectionCount = _bridge.ConnectionCount;

            // The bridge stopped itself (autonomous capture loss, or the notification
            // Stop action) — correct local state to match.
            if (IsOutputActive && !_bridge.IsActive)
            {
                IsOutputActive = false;
                // Deliberately informational, not an error: the bridge stopping itself covers
                // both autonomous capture loss and the notification Stop action, and the latter
                // is a deliberate user action (#327 product note).
                SetStatus("Output stopped");
            }
        });
    }

    /// <summary>Pre-populates the stream name for a resume request without starting output.</summary>
    [RelayCommand]
    private async Task ApplyResumeRequestAsync()
    {
        var state = await _appStateRepo.RestoreStateAsync();
        if (string.IsNullOrWhiteSpace(state.StreamName))
            return;

        StreamName = state.StreamName;
        SetStatus("Tap Start to resume output");
    }

    /// <summary>Applies an inbound re-stream request from a deep link or the Sources page.</summary>
    public void ApplyReStreamRequest(string? sourceId, bool isReStreamMode)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
            return;

        IsReStreamMode = isReStreamMode;
        SelectReStreamSourceById(sourceId);
        StreamName = "NDI-" + new string(sourceId.Where(char.IsLetterOrDigit).Take(32).ToArray());
        SetStatus("Re-stream mode: ready — tap Start to begin.");
    }

    [RelayCommand]
    private async Task ToggleReStreamModeAsync()
    {
        IsReStreamMode = !IsReStreamMode;

        if (IsReStreamMode)
        {
            // When switching to re-stream mode, default stream name uses the source identifier.
            StreamName = "NDI-" + new string(ReStreamSourceId!.Where(char.IsLetterOrDigit).Take(32).ToArray());
            SetStatus("Re-stream mode: select a discovered source on the Sources page.");
        }
        else
        {
            SetStatus("Capture mode: stream your screen or camera as an NDI sender.");
        }

        // Persist mode so it survives app restarts / process death.
        var state = await _appStateRepo.RestoreStateAsync();
        await _appStateRepo.SaveAsync(new AppStateSnapshot(
            state.LastViewerSourceId,
            StreamName,
            false, // isOutputActive resets when switching modes
            state.LastSelectedSourceId));
    }

    /// <summary>
    /// Row tap for the Capture/Re-stream row (#346 OUT-07). Deliberately the same plain flip the
    /// Switch's two-way IsToggled binding performs — not <see cref="ToggleReStreamModeCommand"/>,
    /// which also renames the stream, persists state and requires a selected ReStreamSourceId
    /// (dereferenced with <c>!</c> below and would NRE if it were routed through the row tap).
    /// </summary>
    [RelayCommand]
    private void ToggleOutputMode() => IsReStreamMode = !IsReStreamMode;

    /// <summary>Row tap for the microphone row (#346 OUT-07); disabled while output runs, like the Switch.</summary>
    [RelayCommand(CanExecute = nameof(CanToggleMicrophone))]
    private void ToggleMicrophone() => CaptureMicrophone = !CaptureMicrophone;

    private bool CanToggleMicrophone() => !IsOutputActive;

    [RelayCommand]
    private async Task StartOutputAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(StreamName))
        {
            SetStatus("Please enter a stream name before starting output.", isError: true);
            return;
        }

        if (IsReStreamMode && string.IsNullOrWhiteSpace(ReStreamSourceId))
        {
            SetStatus("Select a source to re-stream before starting output.", isError: true);
            return;
        }

        SetStatus(null);

        try
        {
            if (IsReStreamMode && !string.IsNullOrEmpty(ReStreamSourceId))
            {
                // Start re-streaming from the selected NDI source.
                await _bridge.StartReStreamFromSourceAsync(
                    ReStreamSourceId, QualityProfile.Balanced, cancellationToken);

                IsOutputActive = true;
                SetStatus("Re-stream active");
            }
            else
            {
                // Start capture output. The capture source owns the Android foreground
                // session / permission flow — the VM only talks to the bridge.
                await _bridge.StartOutputAsync(
                    StreamName, SelectedInputKind, CaptureMicrophone, cancellationToken);

                IsOutputActive = true;
                SetStatus("Output active");

                // Persist the configuration only after a successful start.
                await _configRepo.SaveAsync(new OutputConfiguration(
                    StreamName, SelectedInputKind, CaptureMicrophone));
            }

            // Persist output state so it survives resume / process death.
            var snapshot = await _appStateRepo.RestoreStateAsync();
            await _appStateRepo.SaveAsync(new AppStateSnapshot(
                snapshot.LastViewerSourceId,
                StreamName,
                true,
                snapshot.LastSelectedSourceId));
        }
        catch (OperationCanceledException)
        {
            // The user declined the capture permission (e.g. MediaProjection consent).
            IsOutputActive = false;
            SetStatus("Permission declined — output not started.", isError: true);
        }
        catch (Exception ex)
        {
            IsOutputActive = false;
            // Plain-language framing plus a recovery step around the raw reason (#349). The
            // reason is trimmed so a message that already ends in '.' does not produce '..'.
            var reason = ex.Message.TrimEnd('.', ' ');
            SetStatus($"Output failed to start: {reason}. Tap Start to try again.", isError: true);
        }
    }

    [RelayCommand]
    private async Task StopOutputAsync(CancellationToken cancellationToken)
    {
        if (IsReStreamMode)
        {
            await _bridge.StopReStreamAsync(cancellationToken);
        }
        else
        {
            await _bridge.StopOutputAsync(cancellationToken);
        }

        IsOutputActive = false;
        SetStatus(null);
        IsOnProgramTally = false;
        ConnectionCount = 0;

        // Clear output state but keep viewer source.
        var snapshot = await _appStateRepo.RestoreStateAsync();
        await _appStateRepo.SaveAsync(new AppStateSnapshot(
            snapshot.LastViewerSourceId,
            null,
            false,
            snapshot.LastSelectedSourceId));
    }

    /// <summary>Container-owned teardown only (singleton lifetime) — never called from page lifecycle.</summary>
    public void Dispose()
    {
        _lifecycle.AppResumed -= OnAppResumed;
        _bridge.OutputStatusChanged -= OnOutputStatusChanged;
        _discoveryService.SnapshotReady -= OnReStreamSourcesSnapshotReady;
    }
}
