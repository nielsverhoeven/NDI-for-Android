using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiForAndroid.Features.Ptz.Models;
using NdiForAndroid.Features.Ptz.Services;

namespace NdiForAndroid.Features.Ptz.ViewModels;

/// <summary>
/// Editor for a single source's VISCA-over-TCP PTZ override. Owned by <c>ViewerViewModel</c>,
/// which persists <see cref="EndpointSaved"/> through <c>ISourceRepository.SavePtzOverrideAsync</c>
/// and rebuilds its <see cref="IPtzController"/> from the result.
/// </summary>
public sealed partial class PtzEndpointFormViewModel : ObservableObject
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(3);

    private readonly IPtzControllerFactory _controllerFactory;

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private string _host = string.Empty;

    [ObservableProperty]
    private string _portText = PtzEndpoint.DefaultPort.ToString();

    [ObservableProperty]
    private string _validationMessage = string.Empty;

    [ObservableProperty]
    private PtzLinkState _status = PtzLinkState.Disconnected;

    [ObservableProperty]
    private string? _statusText;

    /// <summary>Raised on Save/Clear with the parsed endpoint, or null when the override was cleared.</summary>
    public event EventHandler<PtzEndpoint?>? EndpointSaved;

    public PtzEndpointFormViewModel(IPtzControllerFactory controllerFactory)
    {
        ArgumentNullException.ThrowIfNull(controllerFactory);
        _controllerFactory = controllerFactory;
    }

    /// <summary>Populates the form from the source's current override and opens the dialog.</summary>
    public void Open(string? host, int? port)
    {
        Host = host ?? string.Empty;
        PortText = (port ?? PtzEndpoint.DefaultPort).ToString();
        ValidationMessage = string.Empty;
        Status = PtzLinkState.Disconnected;
        StatusText = null;
        IsOpen = true;
    }

    [RelayCommand]
    private void Save()
    {
        if (!TryParseEndpoint(out var endpoint, out var error))
        {
            ValidationMessage = error;
            return;
        }

        ValidationMessage = string.Empty;
        IsOpen = false;
        EndpointSaved?.Invoke(this, endpoint);
    }

    [RelayCommand]
    private void Clear()
    {
        Host = string.Empty;
        PortText = PtzEndpoint.DefaultPort.ToString();
        ValidationMessage = string.Empty;
        IsOpen = false;
        EndpointSaved?.Invoke(this, null);
    }

    [RelayCommand]
    private void Cancel()
    {
        IsOpen = false;
        ValidationMessage = string.Empty;
        StatusText = null;
    }

    [RelayCommand(AllowConcurrentExecutions = false)]
    private async Task Test()
    {
        if (!TryParseEndpoint(out var endpoint, out var error) || endpoint is null)
        {
            ValidationMessage = error;
            return;
        }

        ValidationMessage = string.Empty;
        StatusText = "Testing...";

        var controller = _controllerFactory.Create(endpoint);
        try
        {
            using var cts = new CancellationTokenSource(TestTimeout);
            await controller.PanTiltAsync(0f, 0f, cts.Token);
            Status = controller.LinkState;
            StatusText = Status == PtzLinkState.Connected ? "Connected." : controller.LastError ?? "Failed to connect.";
        }
        finally
        {
            await controller.ShutdownAsync();
        }
    }

    private bool TryParseEndpoint(out PtzEndpoint? endpoint, out string error)
    {
        var host = Host.Trim();
        if (string.IsNullOrEmpty(host))
        {
            endpoint = null;
            error = "Enter a host.";
            return false;
        }

        var portText = PortText.Trim();
        int port;
        if (string.IsNullOrEmpty(portText))
        {
            port = PtzEndpoint.DefaultPort;
        }
        else if (!int.TryParse(portText, out port) || port is < 1 or > 65535)
        {
            endpoint = null;
            error = "Port must be between 1 and 65535.";
            return false;
        }

        endpoint = new PtzEndpoint(host, port);
        error = string.Empty;
        return true;
    }
}
