using NdiForAndroid.Features.Ptz.Services;

namespace NdiForAndroid.Tests.Features.Ptz.Fakes;

/// <summary>Hand-written <see cref="IViscaTransport"/> test double: records sent commands and lets a test queue up connect/send/receive outcomes.</summary>
public sealed class FakeViscaTransport : IViscaTransport
{
    private readonly Queue<Action> _connectOutcomes = new();
    private readonly Queue<Action> _sendOutcomes = new();
    private readonly Queue<Func<CancellationToken, Task<byte[]>>> _receiveOutcomes = new();
    private int _pendingConnectHangs;

    public int ConnectCount { get; private set; }
    public int DisconnectCount { get; private set; }
    public List<byte[]> SentCommands { get; } = new();

    public bool IsConnected { get; private set; }

    public byte[] DefaultReply { get; set; } = { 0x90, 0x41, 0xFF };

    /// <summary>Number of queued receive outcomes that have not been consumed yet.</summary>
    public int PendingReplyCount => _receiveOutcomes.Count;

    public void EnqueueConnectFailure(Exception exception) => _connectOutcomes.Enqueue(() => throw exception);

    /// <summary>The next connect never completes by itself and ends only when its token is cancelled.</summary>
    public void EnqueueConnectHang() => _pendingConnectHangs++;

    public void EnqueueSendFailure(Exception exception) => _sendOutcomes.Enqueue(() => throw exception);

    public void EnqueueReceiveFailure(Exception exception) =>
        _receiveOutcomes.Enqueue(_ => Task.FromException<byte[]>(exception));

    public void EnqueueReply(byte[] frame) => _receiveOutcomes.Enqueue(_ => Task.FromResult(frame));

    /// <summary>The next receive never completes by itself and ends only when its token is cancelled.</summary>
    public void EnqueueReceiveHang() =>
        _receiveOutcomes.Enqueue(async ct =>
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, ct).ConfigureAwait(false);
            return Array.Empty<byte>();
        });

    /// <summary>
    /// The next receive waits for its token and then throws the supplied exception instead of the
    /// cancellation, like a transport that closes its socket when the token fires.
    /// </summary>
    public void EnqueueReceiveThrowAfterCancel(Func<Exception> exceptionFactory) =>
        _receiveOutcomes.Enqueue(async ct =>
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw exceptionFactory();
            }

            return Array.Empty<byte>();
        });

    public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        ConnectCount++;
        if (_pendingConnectHangs > 0)
        {
            _pendingConnectHangs--;
            return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        if (_connectOutcomes.Count > 0)
            _connectOutcomes.Dequeue().Invoke();

        IsConnected = true;
        return Task.CompletedTask;
    }

    public Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        SentCommands.Add(payload.ToArray());
        if (_sendOutcomes.Count > 0)
            _sendOutcomes.Dequeue().Invoke();

        return Task.CompletedTask;
    }

    public Task<byte[]> ReceiveFrameAsync(CancellationToken cancellationToken = default)
    {
        Func<CancellationToken, Task<byte[]>> outcome = _receiveOutcomes.Count > 0
            ? _receiveOutcomes.Dequeue()
            : _ => Task.FromResult(DefaultReply);
        return outcome(cancellationToken);
    }

    public Task DisconnectAsync()
    {
        DisconnectCount++;
        IsConnected = false;
        return Task.CompletedTask;
    }
}
