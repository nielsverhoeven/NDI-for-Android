using NdiForAndroid.Features.Ptz.Models;

namespace NdiForAndroid.Features.Ptz.Services;

/// <summary>
/// PTZ control over a single persistent VISCA-over-TCP connection. Connects lazily on the first
/// command, serializes connect/send/receive so commands cannot interleave on the shared socket,
/// and reconnects transparently once per command if an already-open connection fails.
/// </summary>
public sealed class ViscaPtzController : IPtzController
{
    private static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan DefaultCommandTimeout = TimeSpan.FromSeconds(2);

    private readonly IViscaTransport _transport;
    private readonly PtzEndpoint _endpoint;
    private readonly TimeSpan _connectTimeout;
    private readonly TimeSpan _commandTimeout;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private readonly object _stateLock = new();
    private PtzLinkState _linkState = PtzLinkState.Disconnected;
    private string? _lastError;

    public ViscaPtzController(
        IViscaTransport transport,
        PtzEndpoint endpoint,
        TimeSpan? connectTimeout = null,
        TimeSpan? commandTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(endpoint);
        _transport = transport;
        _endpoint = endpoint;
        _connectTimeout = connectTimeout ?? DefaultConnectTimeout;
        _commandTimeout = commandTimeout ?? DefaultCommandTimeout;
    }

    public PtzLinkState LinkState { get { lock (_stateLock) return _linkState; } }

    public string? LastError { get { lock (_stateLock) return _lastError; } }

    public event EventHandler<PtzLinkState>? LinkStateChanged;

    public Task<bool> PanTiltAsync(float panSpeed, float tiltSpeed, CancellationToken cancellationToken = default) =>
        SendCommandAsync(ViscaCommandEncoder.PanTiltDrive(panSpeed, tiltSpeed), cancellationToken);

    public Task<bool> ZoomAsync(float zoomSpeed, CancellationToken cancellationToken = default) =>
        SendCommandAsync(ViscaCommandEncoder.ZoomSpeed(zoomSpeed), cancellationToken);

    public Task<bool> AutoFocusAsync(CancellationToken cancellationToken = default) =>
        SendCommandAsync(ViscaCommandEncoder.AutoFocus(), cancellationToken);

    public Task<bool> StorePresetAsync(int presetNumber, CancellationToken cancellationToken = default) =>
        SendCommandAsync(ViscaCommandEncoder.StorePreset(presetNumber), cancellationToken);

    public Task<bool> RecallPresetAsync(int presetNumber, float speed = 1f, CancellationToken cancellationToken = default) =>
        SendCommandAsync(ViscaCommandEncoder.RecallPreset(presetNumber), cancellationToken);

    public async Task ShutdownAsync()
    {
        await _gate.WaitAsync().ConfigureAwait(false);
        try
        {
            await _transport.DisconnectAsync().ConfigureAwait(false);
            SetState(PtzLinkState.Disconnected, error: null);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<bool> SendCommandAsync(byte[] command, CancellationToken cancellationToken)
    {
        try
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return false;
        }

        try
        {
            var wasAlreadyConnected = _transport.IsConnected;
            if (await TrySendAndReceiveAsync(command, cancellationToken).ConfigureAwait(false))
                return true;

            return wasAlreadyConnected && await TrySendAndReceiveAsync(command, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<bool> TrySendAndReceiveAsync(byte[] command, CancellationToken cancellationToken)
    {
        try
        {
            if (!_transport.IsConnected)
            {
                SetState(PtzLinkState.Connecting, error: null);
                using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                connectCts.CancelAfter(_connectTimeout);
                await _transport.ConnectAsync(_endpoint.Host, _endpoint.Port, connectCts.Token).ConfigureAwait(false);
            }

            using var commandCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            commandCts.CancelAfter(_commandTimeout);
            await _transport.SendAsync(command, commandCts.Token).ConfigureAwait(false);
            var reply = ViscaResponseParser.Parse(await _transport.ReceiveFrameAsync(commandCts.Token).ConfigureAwait(false));

            if (reply.Kind == ViscaResponseKind.Error)
            {
                SetState(PtzLinkState.Connected, $"VISCA error 0x{reply.ErrorCode:X2}");
                return false;
            }

            SetState(PtzLinkState.Connected, error: null);
            return reply.Kind is ViscaResponseKind.Ack or ViscaResponseKind.Completion;
        }
        catch (Exception ex) // This seam must never throw to its caller; failures surface via LinkState/LastError instead.
        {
            await _transport.DisconnectAsync().ConfigureAwait(false);
            SetState(PtzLinkState.Error, ex.Message);
            return false;
        }
    }

    private void SetState(PtzLinkState state, string? error)
    {
        bool changed;
        lock (_stateLock)
        {
            changed = _linkState != state;
            _linkState = state;
            _lastError = error;
        }

        if (changed)
            LinkStateChanged?.Invoke(this, state);
    }
}
