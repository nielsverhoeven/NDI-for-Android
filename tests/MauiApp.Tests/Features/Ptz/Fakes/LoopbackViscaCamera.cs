using System.Net;
using System.Net.Sockets;
using Xunit;

namespace NdiForAndroid.Tests.Features.Ptz.Fakes;

public enum LoopbackViscaCameraMode
{
    /// <summary>Replies Ack to every command.</summary>
    Respond,

    /// <summary>Replies Ack, but split across two separate writes.</summary>
    SplitAck,

    /// <summary>Replies with a VISCA error frame to every command.</summary>
    Error,

    /// <summary>Accepts the connection but never writes a reply.</summary>
    Silent,

    /// <summary>Replies Ack to the first command received on a connection, then closes it.</summary>
    DropAfterFirstCommand,
}

/// <summary>Minimal raw-VISCA-over-TCP camera stand-in for loopback integration tests. Listens on an OS-assigned ephemeral port.</summary>
public sealed class LoopbackViscaCamera : IAsyncLifetime
{
    private readonly TcpListener _listener = new(IPAddress.Loopback, 0);
    private readonly CancellationTokenSource _cts = new();
    private Task? _acceptLoop;

    public LoopbackViscaCameraMode Mode { get; set; } = LoopbackViscaCameraMode.Respond;

    public int Port { get; private set; }

    public Task InitializeAsync()
    {
        _listener.Start();
        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
        _acceptLoop = RunAcceptLoopAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _cts.Cancel();
        _listener.Stop();

        if (_acceptLoop is not null)
        {
            try
            {
                await _acceptLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _cts.Dispose();
    }

    private async Task RunAcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;
            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            _ = HandleClientAsync(client, cancellationToken);
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            var stream = client.GetStream();
            try
            {
                if (Mode == LoopbackViscaCameraMode.DropAfterFirstCommand)
                {
                    var command = await ReadCommandAsync(stream, cancellationToken).ConfigureAwait(false);
                    if (command is not null)
                        await stream.WriteAsync(new byte[] { 0x90, 0x41, 0xFF }, cancellationToken).ConfigureAwait(false);
                    return;
                }

                while (!cancellationToken.IsCancellationRequested)
                {
                    var command = await ReadCommandAsync(stream, cancellationToken).ConfigureAwait(false);
                    if (command is null)
                        return;

                    await RespondAsync(stream, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (IOException)
            {
            }
            catch (OperationCanceledException)
            {
            }
        }
    }

    private static async Task<byte[]?> ReadCommandAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var frame = new List<byte>(8);
        var single = new byte[1];
        while (true)
        {
            int bytesRead;
            try
            {
                bytesRead = await stream.ReadAsync(single, cancellationToken).ConfigureAwait(false);
            }
            catch (IOException)
            {
                return null;
            }

            if (bytesRead == 0)
                return null;

            frame.Add(single[0]);
            if (single[0] == 0xFF)
                return frame.ToArray();
        }
    }

    private async Task RespondAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        switch (Mode)
        {
            case LoopbackViscaCameraMode.Silent:
                return;

            case LoopbackViscaCameraMode.Error:
                await stream.WriteAsync(new byte[] { 0x90, 0x60, 0x02, 0xFF }, cancellationToken).ConfigureAwait(false);
                return;

            case LoopbackViscaCameraMode.SplitAck:
                await stream.WriteAsync(new byte[] { 0x90, 0x41 }, cancellationToken).ConfigureAwait(false);
                await Task.Delay(20, cancellationToken).ConfigureAwait(false);
                await stream.WriteAsync(new byte[] { 0xFF }, cancellationToken).ConfigureAwait(false);
                return;

            default:
                await stream.WriteAsync(new byte[] { 0x90, 0x41, 0xFF }, cancellationToken).ConfigureAwait(false);
                return;
        }
    }
}
