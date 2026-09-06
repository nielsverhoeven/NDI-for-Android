namespace NdiForAndroid.Features.Ptz.Services;

/// <summary>Mockable raw-socket seam for VISCA-over-TCP. One instance represents one TCP connection.</summary>
public interface IViscaTransport
{
    /// <summary>True once connected and neither side is known to have closed the socket.</summary>
    bool IsConnected { get; }

    Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default);

    Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default);

    /// <summary>Reads one reply frame, up to and including its terminating 0xFF.</summary>
    Task<byte[]> ReceiveFrameAsync(CancellationToken cancellationToken = default);

    Task DisconnectAsync();
}

/// <summary>Creates a fresh <see cref="IViscaTransport"/> per PTZ backend instance.</summary>
public interface IViscaTransportFactory
{
    IViscaTransport Create();
}
