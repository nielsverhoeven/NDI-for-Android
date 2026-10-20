using System.Net.Sockets;
using NdiForAndroid.Features.Ptz.Models;

namespace NdiForAndroid.Features.Ptz.Services;

/// <summary>
/// PTZ control over a single persistent VISCA-over-TCP connection. Connects lazily on the first
/// command and serializes connect/send/receive so commands cannot interleave on the shared socket.
/// Replies are matched by VISCA socket, so a late Completion or Error of an earlier command is
/// never taken as the current command's reply. A transport failure on an already-open connection
/// is retried once on a fresh connection; a camera reply, including Error, is never re-sent.
/// </summary>
public sealed class ViscaPtzController : IPtzController
{
    /// <summary>How long a connect attempt may take before it is reported as timed out.</summary>
    public static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromSeconds(3);

    /// <summary>How long a send plus its reply may take before the camera is reported as not answering.</summary>
    public static readonly TimeSpan DefaultCommandTimeout = TimeSpan.FromSeconds(2);

    private const int MaxFramesPerReply = 8;
    private const string UnexpectedRepliesMessage = "Camera sent unexpected replies.";

    private readonly IViscaTransport _transport;
    private readonly PtzEndpoint _endpoint;
    private readonly TimeSpan _connectTimeout;
    private readonly TimeSpan _commandTimeout;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private readonly object _stateLock = new();
    private PtzLinkState _linkState = PtzLinkState.Disconnected;
    private string? _lastError;

    // Bit y set: the camera ACKed a command on VISCA socket y and its Completion/Error is unread. Touched only under _gate; cleared on connect, disconnect and Shutdown.
    private int _pendingSocketMask;

    private enum AttemptOutcome { Succeeded, RejectedByCamera, TransportFailed }

    private enum AttemptPhase { Connect, Command }

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
            _pendingSocketMask = 0;
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
            var outcome = await TrySendAndReceiveAsync(command, cancellationToken).ConfigureAwait(false);
            if (outcome == AttemptOutcome.Succeeded)
                return true;

            if (outcome == AttemptOutcome.RejectedByCamera || !wasAlreadyConnected)
                return false;

            return await TrySendAndReceiveAsync(command, cancellationToken).ConfigureAwait(false) == AttemptOutcome.Succeeded;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<AttemptOutcome> TrySendAndReceiveAsync(byte[] command, CancellationToken cancellationToken)
    {
        var phase = AttemptPhase.Connect;
        CancellationTokenSource? phaseCts = null;
        try
        {
            if (!_transport.IsConnected)
            {
                SetState(PtzLinkState.Connecting, error: null);
                _pendingSocketMask = 0;
                phaseCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                phaseCts.CancelAfter(_connectTimeout);
                await _transport.ConnectAsync(_endpoint.Host, _endpoint.Port, phaseCts.Token).ConfigureAwait(false);
                phaseCts.Dispose();
                phaseCts = null;
            }

            phase = AttemptPhase.Command;
            phaseCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            phaseCts.CancelAfter(_commandTimeout);
            await _transport.SendAsync(command, phaseCts.Token).ConfigureAwait(false);
            var reply = await ReadReplyForCurrentCommandAsync(phaseCts.Token).ConfigureAwait(false);

            if (reply.Kind == ViscaResponseKind.Error)
            {
                SetState(PtzLinkState.Connected, $"VISCA error 0x{reply.ErrorCode:X2}");
                return AttemptOutcome.RejectedByCamera;
            }

            SetState(PtzLinkState.Connected, error: null);
            return AttemptOutcome.Succeeded;
        }
        catch (Exception ex) // This seam must never throw to its caller; failures surface via LinkState/LastError instead.
        {
            var timedOut = ex is OperationCanceledException || phaseCts?.IsCancellationRequested == true;
            await _transport.DisconnectAsync().ConfigureAwait(false);
            _pendingSocketMask = 0;
            SetState(PtzLinkState.Error, DescribeFailure(phase, ex, timedOut));
            return AttemptOutcome.TransportFailed;
        }
        finally
        {
            phaseCts?.Dispose();
        }
    }

    /// <summary>
    /// Reads frames until one is the reply to the command just sent. An ACK is the reply and marks its
    /// socket pending. A Completion or Error on a pending socket belongs to an earlier ACKed command: it
    /// is consumed and clears the socket. An Error on socket 0 or on a socket that is not pending, or a
    /// Completion on a socket that is not pending (a camera that skips the ACK), is the reply. Unknown
    /// frames are skipped. After <see cref="MaxFramesPerReply"/> frames without a reply the stream is
    /// treated as out of step and an <see cref="InvalidDataException"/> makes the caller reconnect.
    /// </summary>
    private async Task<ViscaResponse> ReadReplyForCurrentCommandAsync(CancellationToken cancellationToken)
    {
        for (var framesRead = 1; framesRead <= MaxFramesPerReply; framesRead++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var reply = ViscaResponseParser.Parse(await _transport.ReceiveFrameAsync(cancellationToken).ConfigureAwait(false));
            var socketBit = reply.Socket is > 0 and < 16 ? 1 << reply.Socket : 0;
            var isPendingSocket = socketBit != 0 && (_pendingSocketMask & socketBit) != 0;
            switch (reply.Kind)
            {
                case ViscaResponseKind.Ack:
                    _pendingSocketMask |= socketBit;
                    return reply;

                case ViscaResponseKind.Completion or ViscaResponseKind.Error when isPendingSocket:
                    _pendingSocketMask &= ~socketBit;
                    continue;

                case ViscaResponseKind.Completion:
                case ViscaResponseKind.Error:
                    return reply;

                default:
                    continue;
            }
        }

        throw new InvalidDataException(UnexpectedRepliesMessage);
    }

    private string DescribeFailure(AttemptPhase phase, Exception ex, bool timedOut)
    {
        var endpoint = $"{_endpoint.Host}:{_endpoint.Port}";
        if (phase == AttemptPhase.Connect)
        {
            if (timedOut)
                return $"No connection to {endpoint} (timed out).";

            return ex is SocketException socketException
                ? $"Cannot reach {endpoint} ({DescribeSocketError(socketException.SocketErrorCode)})."
                : ex.Message;
        }

        if (timedOut)
            return "Camera did not answer (timed out).";

        return ex is IOException or SocketException or ObjectDisposedException
            ? "Connection to the camera was lost."
            : ex.Message;
    }

    private static string DescribeSocketError(SocketError error) => error switch
    {
        SocketError.ConnectionRefused => "connection refused",
        SocketError.HostNotFound or SocketError.NoData or SocketError.TryAgain => "host not found",
        SocketError.HostUnreachable or SocketError.NetworkUnreachable or SocketError.HostDown or SocketError.NetworkDown => "unreachable",
        SocketError.TimedOut => "timed out",
        _ => error.ToString(),
    };

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
