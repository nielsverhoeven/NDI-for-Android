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
/// <remarks>
/// <para>
/// <b>Save/Clear/Test semantics (#374).</b> Before this fix, "Clear" reset the two fields AND
/// immediately closed the dialog and raised <see cref="EndpointSaved"/> with null — silently
/// persisting the removal of any existing override with the same one-tap, no-confirmation path
/// as "Save", even though it sits next to the non-destructive "Test" button and reads like a
/// field-reset action. <see cref="Clear"/> now only resets the two fields for further editing:
/// it never closes the dialog and never raises <see cref="EndpointSaved"/>.
/// </para>
/// <para>
/// A blank host is a deliberate, valid state — the Host field's own placeholder
/// ("Host (blank = use NDI PTZ)") has always documented it as meaning "use the source's built-in
/// NDI PTZ" — so <see cref="Save"/> commits a blank host as "no override" (<c>endpoint = null</c>)
/// exactly like a non-blank host commits a VISCA endpoint. This is what makes an override
/// removable again after the Clear semantics change: tap Clear to blank the fields, then Save to
/// commit the removal (or edit the fields further before saving). <see cref="Test"/> is
/// unaffected by any of this — a blank host is still not testable (there is nothing to connect
/// to), so it keeps failing with <see cref="HostRequiredMessage"/>, unchanged from before.
/// </para>
/// <para>
/// <see cref="SaveCommand"/> is additionally disabled — not just click-time-validated — while the
/// port is out of range, so a malformed value can never be committed (Nielsen H5, error
/// prevention); <see cref="PortValidationMessage"/> updates live as the user types, ahead of any
/// Save/Test attempt.
/// </para>
/// </remarks>
public sealed partial class PtzEndpointFormViewModel : ObservableObject
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Shown when Test (or, before the #374 fix, Save) is attempted with a blank host. Wording
    /// per #370 sources-viewer-007: explains both the "enter a host to test" path and the
    /// "tap Clear then Save" path back to the source's built-in NDI PTZ.
    /// </summary>
    public const string HostRequiredMessage =
        "Enter a host to test a custom PTZ endpoint, or tap Clear to use the source's built-in NDI PTZ instead of testing/saving.";

    public const string PortRangeMessage = "Port must be between 1 and 65535.";

    private readonly IPtzControllerFactory _controllerFactory;

    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _host = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    private string _portText = PtzEndpoint.DefaultPort.ToString();

    /// <summary>Result of the last Save/Test attempt, or a Test connection failure.</summary>
    [ObservableProperty]
    private string _validationMessage = string.Empty;

    /// <summary>
    /// Live, per-keystroke port validity feedback (#374 error prevention) — independent of
    /// <see cref="ValidationMessage"/>, which only updates on a Save/Test attempt.
    /// </summary>
    [ObservableProperty]
    private string _portValidationMessage = string.Empty;

    [ObservableProperty]
    private PtzLinkState _status = PtzLinkState.Disconnected;

    [ObservableProperty]
    private string? _statusText;

    /// <summary>Raised on Save with the parsed endpoint, or null when the host was left blank (no override).</summary>
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
        PortValidationMessage = string.Empty;
        Status = PtzLinkState.Disconnected;
        StatusText = null;
        IsOpen = true;
    }

    [RelayCommand(CanExecute = nameof(CanSave))]
    private void Save()
    {
        // CanSave gates the bound Save button, but ICommand.Execute can still be invoked directly
        // (tests do exactly this), so re-check here too — never commit an out-of-range port. A
        // blank host is always valid (it commits "no override" — see the class remarks).
        if (!IsPortValid(PortText))
        {
            PortValidationMessage = PortRangeMessage;
            return;
        }

        var host = Host.Trim();
        PtzEndpoint? endpoint = string.IsNullOrEmpty(host)
            ? null
            : new PtzEndpoint(host, ParsePortOrDefault(PortText));

        ValidationMessage = string.Empty;
        IsOpen = false;
        EndpointSaved?.Invoke(this, endpoint);
    }

    private bool CanSave() => IsPortValid(PortText);

    /// <summary>
    /// Resets the two fields for further editing only — does NOT close the dialog and does NOT
    /// raise <see cref="EndpointSaved"/> (#374). Tap Save afterwards to actually commit the
    /// resulting blank-host "no override" state, or Cancel to discard the reset.
    /// </summary>
    [RelayCommand]
    private void Clear()
    {
        Host = string.Empty;
        PortText = PtzEndpoint.DefaultPort.ToString();
        ValidationMessage = string.Empty;
        PortValidationMessage = string.Empty;
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
        // Test semantics are unchanged by #374: a blank host is still not testable (nothing to
        // connect to), unlike Save, which now treats it as a deliberate "no override" commit.
        if (!TryParseEndpointForTest(out var endpoint, out var error) || endpoint is null)
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

    partial void OnPortTextChanged(string value) =>
        PortValidationMessage = IsPortValid(value) ? string.Empty : PortRangeMessage;

    private bool TryParseEndpointForTest(out PtzEndpoint? endpoint, out string error)
    {
        var host = Host.Trim();
        if (string.IsNullOrEmpty(host))
        {
            endpoint = null;
            error = HostRequiredMessage;
            return false;
        }

        if (!IsPortValid(PortText))
        {
            endpoint = null;
            error = PortRangeMessage;
            return false;
        }

        endpoint = new PtzEndpoint(host, ParsePortOrDefault(PortText));
        error = string.Empty;
        return true;
    }

    private static bool IsPortValid(string portText)
    {
        var trimmed = portText.Trim();
        return string.IsNullOrEmpty(trimmed) || (int.TryParse(trimmed, out var port) && port is >= 1 and <= 65535);
    }

    private static int ParsePortOrDefault(string portText)
    {
        var trimmed = portText.Trim();
        return string.IsNullOrEmpty(trimmed) ? PtzEndpoint.DefaultPort : int.Parse(trimmed);
    }
}
