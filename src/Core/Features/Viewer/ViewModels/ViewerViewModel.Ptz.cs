using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiForAndroid.Features.Ptz.Models;
using NdiForAndroid.Features.Ptz.Services;
using NdiForAndroid.Features.Ptz.ViewModels;
using NdiForAndroid.Features.Sources.Models;
using NdiForAndroid.Services;

namespace NdiForAndroid.Features.Viewer.ViewModels;

public partial class ViewerViewModel
{
    private IPtzController? _ptzController;
    private NdiSource? _activeSource;

    public PtzEndpointFormViewModel PtzEndpointForm { get; }

    [ObservableProperty]
    private PtzLinkState _ptzLinkState = PtzLinkState.Disconnected;

    [ObservableProperty]
    private string? _ptzStatusText;

    public static IReadOnlyList<int> PresetNumbers { get; } = Enumerable.Range(1, 8).ToArray();

    [ObservableProperty]
    private string? _ptzPresetStatusMessage;

    private ITimer? _presetStatusTimer;
    private static readonly TimeSpan PresetStatusDisplayDuration = TimeSpan.FromSeconds(2);

    /// <summary>True when the active source has a VISCA endpoint override configured.</summary>
    [ObservableProperty]
    private bool _hasPtzOverride;

    /// <summary>The VISCA endpoint currently attached (null while NDI-native PTZ is in use).</summary>
    private PtzEndpoint? _ptzEndpoint;

    /// <summary>
    /// Gates the pad/zoom/preset controls: available for NDI-native PTZ or whenever a VISCA
    /// endpoint is configured. The VISCA link is established lazily on the first command, so
    /// gating on <see cref="PtzLinkState.Connected"/> would hide the pad forever.
    /// </summary>
    public bool IsPtzControlActive => IsPtzSupported || HasPtzOverride;

    partial void OnIsPtzSupportedChanged(bool value) => OnPropertyChanged(nameof(IsPtzControlActive));

    partial void OnHasPtzOverrideChanged(bool value) => OnPropertyChanged(nameof(IsPtzControlActive));

    partial void StartPtz(NdiSource? source)
    {
        _activeSource = source;
        var endpoint = BuildPtzEndpoint(source);
        HasPtzOverride = endpoint is not null;
        AttachPtzController(_ptzControllerFactory.Create(endpoint), endpoint);
    }

    partial void StopPtz()
    {
        DetachPtzController();
        _activeSource = null;
        HasPtzOverride = false;
        PtzLinkState = PtzLinkState.Disconnected;
        PtzStatusText = null;
    }

    partial void DisposePtz()
    {
        DetachPtzController();
        _presetStatusTimer?.Dispose();
        PtzEndpointForm.EndpointSaved -= OnPtzEndpointSaved;
    }

    /// <summary>Short pan/tilt burst: run at ±<see cref="PtzNudgeSpeed"/> for 250 ms, then stop.</summary>
    [RelayCommand]
    private async Task PtzNudge(string? direction)
    {
        NotifyControlInteraction();
        var (pan, tilt) = direction switch
        {
            "left" => (-PtzNudgeSpeed, 0f),
            "right" => (PtzNudgeSpeed, 0f),
            "up" => (0f, PtzNudgeSpeed),
            "down" => (0f, -PtzNudgeSpeed),
            _ => (0f, 0f),
        };
        if (pan == 0f && tilt == 0f)
            return;

        var controller = GetOrCreatePtzController();
        await controller.PanTiltAsync(pan, tilt);
        await Task.Delay(PtzNudgeDurationMs);
        await controller.PanTiltAsync(0f, 0f);
    }

    /// <summary>Short zoom burst: run at ±<see cref="PtzNudgeSpeed"/> for 250 ms, then stop.</summary>
    [RelayCommand]
    private async Task PtzZoomNudge(string? direction)
    {
        NotifyControlInteraction();
        var speed = direction switch
        {
            "in" => PtzNudgeSpeed,
            "out" => -PtzNudgeSpeed,
            _ => 0f,
        };
        if (speed == 0f)
            return;

        var controller = GetOrCreatePtzController();
        await controller.ZoomAsync(speed);
        await Task.Delay(PtzNudgeDurationMs);
        await controller.ZoomAsync(0f);
    }

    [RelayCommand]
    private async Task PtzAutoFocus()
    {
        NotifyControlInteraction();
        await GetOrCreatePtzController().AutoFocusAsync();
    }

    [RelayCommand]
    private async Task PtzStorePreset(int presetNumber)
    {
        NotifyControlInteraction();
        await GetOrCreatePtzController().StorePresetAsync(presetNumber);

        PtzPresetStatusMessage = $"Preset {presetNumber} stored";
        _presetStatusTimer?.Dispose();
        _presetStatusTimer = _timeProvider.CreateTimer(
            _ => _dispatcher.BeginInvokeOnMainThread(() => PtzPresetStatusMessage = null),
            null, PresetStatusDisplayDuration, Timeout.InfiniteTimeSpan);
    }

    [RelayCommand]
    private async Task PtzRecallPreset(int presetNumber)
    {
        NotifyControlInteraction();
        await GetOrCreatePtzController().RecallPresetAsync(presetNumber);
    }

    [RelayCommand]
    private void OpenPtzEndpointForm() =>
        PtzEndpointForm.Open(_activeSource?.PtzOverrideHost, _activeSource?.PtzOverridePort);

    // async void is intentional: event handler, same pattern as OnAppResumed elsewhere in this file.
    private async void OnPtzEndpointSaved(object? sender, PtzEndpoint? endpoint)
    {
        try
        {
            if (!string.IsNullOrEmpty(SourceId))
                await _sourceRepository.SavePtzOverrideAsync(SourceId, endpoint);
        }
        catch { /* Silent fail — endpoint edit is non-critical; status indicator reflects the retry. */ }

        if (_activeSource is not null)
            _activeSource = _activeSource with { PtzOverrideHost = endpoint?.Host, PtzOverridePort = endpoint?.Port };

        HasPtzOverride = endpoint is not null;
        var controller = _ptzControllerFactory.Create(endpoint);
        AttachPtzController(controller, endpoint);

        // Establish the VISCA link right away (a harmless pan/tilt stop) so the status
        // indicator reflects reachability without waiting for the first real command.
        if (endpoint is not null)
            controller.PanTiltAsync(0f, 0f).FireAndForget();
    }

    private IPtzController GetOrCreatePtzController()
    {
        if (_ptzController is null)
        {
            var endpoint = BuildPtzEndpoint(_activeSource);
            AttachPtzController(_ptzControllerFactory.Create(endpoint), endpoint);
        }

        return _ptzController!;
    }

    private void AttachPtzController(IPtzController controller, PtzEndpoint? endpoint)
    {
        DetachPtzController();
        _ptzController = controller;
        _ptzEndpoint = endpoint;
        controller.LinkStateChanged += OnPtzControllerLinkStateChanged;
        PtzLinkState = controller.LinkState;
        PtzStatusText = DescribePtzLinkState(controller.LinkState);
    }

    private void DetachPtzController()
    {
        if (_ptzController is null)
            return;

        _ptzController.LinkStateChanged -= OnPtzControllerLinkStateChanged;
        _ptzController.ShutdownAsync().FireAndForget();
        _ptzController = null;
        _ptzEndpoint = null;
    }

    private void OnPtzControllerLinkStateChanged(object? sender, PtzLinkState state) =>
        _dispatcher.BeginInvokeOnMainThread(() =>
        {
            PtzLinkState = state;
            PtzStatusText = DescribePtzLinkState(state);
        });

    private static PtzEndpoint? BuildPtzEndpoint(NdiSource? source) =>
        !string.IsNullOrWhiteSpace(source?.PtzOverrideHost)
            ? new PtzEndpoint(source.PtzOverrideHost!.Trim(), source.PtzOverridePort ?? PtzEndpoint.DefaultPort)
            : null;

    private string DescribePtzLinkState(PtzLinkState state)
    {
        if (_ptzEndpoint is null)
            return "PTZ: Using NDI";

        var link = state switch
        {
            PtzLinkState.Connected => "connected",
            PtzLinkState.Connecting => "connecting...",
            PtzLinkState.Error => "error",
            _ => "not connected",
        };

        return $"PTZ: VISCA {_ptzEndpoint.Host}:{_ptzEndpoint.Port} ({link})";
    }
}
