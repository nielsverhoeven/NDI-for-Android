using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace ViscaMockCamera;

internal sealed class TcpViscaListener : IAsyncDisposable
{
    private readonly TcpListener _listener;
    private readonly CameraState _state;
    private readonly Logger _logger;
    private readonly int _port;
    private readonly ConcurrentBag<Task> _clientTasks = new();

    public TcpViscaListener(int port, CameraState state, Logger logger)
    {
        _port = port;
        _state = state;
        _logger = logger;
        _listener = new TcpListener(IPAddress.Any, port);
    }

    public Task RunAsync(CancellationToken cancellationToken) => AcceptLoopAsync(cancellationToken);

    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        _listener.Start();
        _logger.Verbose($"TCP:{_port} listening");

        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                _clientTasks.Add(HandleClientAsync(client, cancellationToken));
            }
        }
        finally
        {
            _listener.Stop();
        }
    }

    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        var remote = client.Client.RemoteEndPoint;
        _logger.Verbose($"TCP:{_port} client connected: {remote}");

        using (client)
        {
            var stream = client.GetStream();
            var readBuffer = new byte[4096];
            var frameBuffer = new List<byte>();

            while (!cancellationToken.IsCancellationRequested)
            {
                int read;
                try
                {
                    read = await stream.ReadAsync(readBuffer, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
                {
                    break;
                }

                if (read == 0)
                {
                    break;
                }

                frameBuffer.AddRange(new ArraySegment<byte>(readBuffer, 0, read));

                foreach (var frame in ViscaFrameSplitter.Extract(frameBuffer))
                {
                    var result = ViscaCommandProcessor.Process(frame, _state);
                    _logger.Info($"TCP:{_port} {remote} recv {ToHex(frame)} -> {result.Description}");

                    var writeFailed = false;
                    foreach (var reply in result.Replies)
                    {
                        try
                        {
                            await stream.WriteAsync(reply, cancellationToken).ConfigureAwait(false);
                            _logger.Verbose($"TCP:{_port} {remote} sent {ToHex(reply)}");
                        }
                        catch (Exception ex) when (ex is IOException or OperationCanceledException or ObjectDisposedException)
                        {
                            _logger.Verbose($"TCP:{_port} {remote} write failed: {ex.Message}");
                            writeFailed = true;
                            break;
                        }
                    }

                    if (writeFailed)
                    {
                        return;
                    }
                }
            }
        }

        _logger.Verbose($"TCP:{_port} client disconnected: {remote}");
    }

    private static string ToHex(byte[] bytes) => Convert.ToHexString(bytes);

    public async ValueTask DisposeAsync()
    {
        _listener.Stop();
        await Task.WhenAll(_clientTasks).ConfigureAwait(false);
    }
}
