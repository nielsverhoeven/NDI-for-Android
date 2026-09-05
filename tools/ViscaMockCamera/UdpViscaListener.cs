using System.Net;
using System.Net.Sockets;

namespace ViscaMockCamera;

internal sealed class UdpViscaListener : IDisposable
{
    private readonly UdpClient _client;
    private readonly CameraState _state;
    private readonly Logger _logger;
    private readonly int _port;
    private readonly Dictionary<IPEndPoint, List<byte>> _perClientBuffers = new();

    public UdpViscaListener(int port, CameraState state, Logger logger)
    {
        _port = port;
        _state = state;
        _logger = logger;
        _client = new UdpClient(port);
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.Verbose($"UDP:{_port} listening");

        while (!cancellationToken.IsCancellationRequested)
        {
            UdpReceiveResult result;
            try
            {
                result = await _client.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException)
            {
                break;
            }

            if (!_perClientBuffers.TryGetValue(result.RemoteEndPoint, out var buffer))
            {
                buffer = new List<byte>();
                _perClientBuffers[result.RemoteEndPoint] = buffer;
            }

            buffer.AddRange(result.Buffer);

            foreach (var frame in ViscaFrameSplitter.Extract(buffer))
            {
                var commandResult = ViscaCommandProcessor.Process(frame, _state);
                _logger.Info($"UDP:{_port} {result.RemoteEndPoint} recv {ToHex(frame)} -> {commandResult.Description}");

                foreach (var reply in commandResult.Replies)
                {
                    await _client.SendAsync(reply, reply.Length, result.RemoteEndPoint).ConfigureAwait(false);
                    _logger.Verbose($"UDP:{_port} {result.RemoteEndPoint} sent {ToHex(reply)}");
                }
            }
        }

        _logger.Verbose($"UDP:{_port} listener stopped");
    }

    private static string ToHex(byte[] bytes) => Convert.ToHexString(bytes);

    public void Dispose() => _client.Dispose();
}
