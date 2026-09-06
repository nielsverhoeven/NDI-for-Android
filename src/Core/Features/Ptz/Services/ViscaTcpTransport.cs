using System.Net.Sockets;

namespace NdiForAndroid.Features.Ptz.Services;

/// <summary>
/// Raw VISCA-over-TCP transport (PTZOptics/Avonic-style; no Sony VISCA-over-IP UDP header), backed
/// by <see cref="TcpClient"/>.
/// </summary>
public sealed class ViscaTcpTransport : IViscaTransport
{
    private TcpClient? _client;
    private NetworkStream? _stream;

    // TcpClient does not probe the connection, so after a peer-initiated close this can stay true
    // until the next read or write attempt fails.
    public bool IsConnected => _client?.Connected == true && _stream is not null;

    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        await DisconnectAsync().ConfigureAwait(false);

        var client = new TcpClient();
        try
        {
            await client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            client.Dispose();
            throw;
        }

        _client = client;
        _stream = client.GetStream();
    }

    public async Task SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        if (_stream is null)
            throw new InvalidOperationException("VISCA transport is not connected.");

        await _stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    public async Task<byte[]> ReceiveFrameAsync(CancellationToken cancellationToken = default)
    {
        if (_client is null || _stream is null)
            throw new InvalidOperationException("VISCA transport is not connected.");

        var stream = _stream;
        var client = _client;
        // NetworkStream.ReadAsync cancellation is not reliably observed on every platform; force it by closing the socket.
        using var registration = cancellationToken.Register(() => client.Close());

        var frame = new List<byte>(8);
        var single = new byte[1];
        while (true)
        {
            var bytesRead = await stream.ReadAsync(single, cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
                throw new IOException("VISCA connection closed by the peer.");

            frame.Add(single[0]);
            if (single[0] == 0xFF)
                return frame.ToArray();
        }
    }

    public Task DisconnectAsync()
    {
        _stream?.Dispose();
        _stream = null;
        _client?.Close();
        _client?.Dispose();
        _client = null;
        return Task.CompletedTask;
    }
}

/// <inheritdoc cref="IViscaTransportFactory"/>
public sealed class ViscaTransportFactory : IViscaTransportFactory
{
    public IViscaTransport Create() => new ViscaTcpTransport();
}
