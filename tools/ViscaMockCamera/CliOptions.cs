namespace ViscaMockCamera;

internal sealed class CliOptions
{
    private CliOptions(IReadOnlyList<int> ports, bool udp, bool verbose, string? logFile)
    {
        Ports = ports;
        Udp = udp;
        Verbose = verbose;
        LogFile = logFile;
    }

    public IReadOnlyList<int> Ports { get; }

    public bool Udp { get; }

    public bool Verbose { get; }

    public string? LogFile { get; }

    public static CliOptions Parse(string[] args)
    {
        var ports = new List<int>();
        var udp = false;
        var verbose = false;
        string? logFile = null;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--port":
                    ports.Add(ReadIntArgument(args, ref i, "--port"));
                    break;
                case "--udp":
                    udp = true;
                    break;
                case "--verbose":
                    verbose = true;
                    break;
                case "--log":
                    logFile = ReadStringArgument(args, ref i, "--log");
                    break;
                default:
                    throw new ArgumentException($"unrecognized argument: {args[i]}");
            }
        }

        if (ports.Count == 0)
        {
            ports.Add(5678);
        }

        return new CliOptions(ports, udp, verbose, logFile);
    }

    private static int ReadIntArgument(string[] args, ref int index, string name)
    {
        var value = ReadStringArgument(args, ref index, name);
        if (!int.TryParse(value, out var result))
        {
            throw new ArgumentException($"{name} requires a numeric value");
        }

        return result;
    }

    private static string ReadStringArgument(string[] args, ref int index, string name)
    {
        if (++index >= args.Length)
        {
            throw new ArgumentException($"{name} requires a value");
        }

        return args[index];
    }
}
