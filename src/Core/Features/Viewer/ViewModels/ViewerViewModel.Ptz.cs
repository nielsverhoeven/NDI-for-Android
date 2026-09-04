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

    [ObservableProperty]
    private string _ptzPresetNumber = "0";

    /// <summary>Gates the pad/zoom/preset controls: available for NDI-native PTZ or once a VISCA endpoint is connected.</summary>
    public bool IsPtzControlActive => IsPtzSupported || PtzLinkState == PtzLinkState.Connected;

    partial void OnIsPtzSupportedChanged(bool value) => OnPropertyChanged(nameof(IsPtzControlActive));

    partial void OnPtzLinkStateChanged(PtzLinkState value) => OnPropertyChanged(nameof(IsPtzControlActive));

    partial void StartPtz(NdiSource? source)
    {
        _activeSource = source;
        AttachPtzController(_ptzControllerFactory.Create(BuildPtzEndpoint(source)));
    }

    partial void StopPtz()
    {
        DetachPtzController();
        _activeSource = null;
        PtzLinkState = PtzLinkState.Disconnected;
        PtzStatusText = null;
    }

    partial void DisposePtz()
    {
        DetachPtzController();
        PtzEndpointForm.EndpointSaved -= OnPtzEndpointSaved;
    }

    /// <summary>Short pan/tilt burst: run at ±<see cref="PtzNudgeSpeed"/> for 250 ms, then stop.</summary>
    [RelayCommand]
    private async Task PtzNudge(string? direction)
    {
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
    private async Task PtzAutoFocus() => await GetOrCreatePtzController().AutoFocusAsync();

    [RelayCommand]
    private async Task PtzStorePreset()
    {
        if (!int.TryParse(PtzPresetNumber, out var presetNo))
            return;

        await GetOrCreatePtzController().StorePresetAsync(presetNo);
    }

    [RelayCommand]
    private async Task PtzRecallPreset()
    {
        if (!int.TryParse(PtzPresetNumber, out var presetNo))
            return;

        await GetOrCreatePtzController().RecallPresetAsync(presetNo);
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

        AttachPtzController(_ptzControllerFactory.Create(endpoint));
    }

    private IPtzController GetOrCreatePtzController()
    {
        if (_ptzController is null)
            AttachPtzController(_ptzControllerFactory.Create(BuildPtzEndpoint(_activeSource)));

        return _ptzController!;
    }

    private void AttachPtzController(IPtzController controller)
    {
        DetachPtzController();
        _ptzController = controller;
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

    private static string DescribePtzLinkState(PtzLinkState state) => state switch
    {
        PtzLinkState.Connected => "PTZ: Connected",
        PtzLinkState.Connecting => "PTZ: Connecting...",
        PtzLinkState.Error => "PTZ: Connection error",
        _ => "PTZ: Using NDI",
    };
}
