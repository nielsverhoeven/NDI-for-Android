using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using ViscaMockCamera;

CliOptions options;
try
{
    options = CliOptions.Parse(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine("usage: ViscaMockCamera [--port <n>]... [--udp] [--verbose] [--log <file>]");
    return 1;
}

using var logger = new Logger(options.Verbose, options.LogFile);
using var state = new CameraState();
using var cts = new CancellationTokenSource();

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

logger.Info("VISCA mock camera starting");
foreach (var address in GetLocalIPv4Addresses())
{
    logger.Info($"listening address: {address}");
}

logger.Info($"ports: {string.Join(", ", options.Ports)} (tcp{(options.Udp ? "+udp" : string.Empty)})");

var tcpListeners = options.Ports.Select(port => new TcpViscaListener(port, state, logger)).ToList();
var udpListeners = options.Udp
    ? options.Ports.Select(port => new UdpViscaListener(port, state, logger)).ToList()
    : new List<UdpViscaListener>();

var tasks = new List<Task>();
tasks.AddRange(tcpListeners.Select(l => l.RunAsync(cts.Token)));
tasks.AddRange(udpListeners.Select(l => l.RunAsync(cts.Token)));

await Task.WhenAll(tasks).ConfigureAwait(false);

foreach (var listener in tcpListeners)
{
    await listener.DisposeAsync().ConfigureAwait(false);
}

foreach (var listener in udpListeners)
{
    listener.Dispose();
}

logger.Info("VISCA mock camera stopped");
return 0;

static IEnumerable<string> GetLocalIPv4Addresses()
{
    return NetworkInterface.GetAllNetworkInterfaces()
        .Where(ni => ni.OperationalStatus == OperationalStatus.Up)
        .SelectMany(ni => ni.GetIPProperties().UnicastAddresses)
        .Where(addr => addr.Address.AddressFamily == AddressFamily.InterNetwork)
        .Select(addr => addr.Address.ToString());
}
