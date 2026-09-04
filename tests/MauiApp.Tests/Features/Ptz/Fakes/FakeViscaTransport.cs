using NdiForAndroid.Features.Ptz.Services;

namespace NdiForAndroid.Tests.Features.Ptz.Fakes;

/// <summary>Hand-written <see cref="IViscaTransport"/> test double: records sent commands and lets a test queue up connect/send/receive outcomes.</summary>
public sealed class FakeViscaTransport : IViscaTransport
{
    private readonly Queue<Action> _connectOutcomes = new();
    private readonly Queue<Action> _sendOutcomes = new();
    private readonly Queue<Func<byte[]>> _receiveOutcomes = new();

    public int ConnectCount { get; private set; }
    public int DisconnectCount { get; private set; }
    public List<byte[]> SentCommands { get; } = new();

    public bool IsConnected { get; private set; }

    public byte[] DefaultReply { get; set; } = { 0x90, 0x41, 0xFF };

    public void EnqueueConnectFailure(Exception exception) => _connectOutcomes.Enqueue(() => throw exception);

    public void EnqueueSendFailure(Exception exception) => _sendOutcomes.Enqueue(() => throw exception);

    public void EnqueueReceiveFailure(Exception exception) => _receiveOutcomes.Enqueue(() => throw exception);

    public void EnqueueReply(byte[] frame) => _receiveOutcomes.Enqueue(() => frame);

    public Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        ConnectCount++;
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
        var outcome = _receiveOutcomes.Count > 0 ? _receiveOutcomes.Dequeue() : () => DefaultReply;
        return Task.FromResult(outcome());
    }

    public Task DisconnectAsync()
    {
        DisconnectCount++;
        IsConnected = false;
        return Task.CompletedTask;
    }
}
