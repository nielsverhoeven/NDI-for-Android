using System.Net.Sockets;

namespace NdiForAndroid.NdiBridge;

/// <summary>TCP reachability probe used for discovery-server health checks.</summary>
internal static class NetworkReachability
{
    public static async Task<bool> IsTcpReachableAsync(string host, int port, CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            await client.ConnectAsync(host, port, timeout.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
