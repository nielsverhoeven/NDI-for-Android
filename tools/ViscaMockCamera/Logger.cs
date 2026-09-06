namespace ViscaMockCamera;

internal sealed class Logger : IDisposable
{
    private readonly object _lock = new();
    private readonly bool _verbose;
    private readonly StreamWriter? _fileWriter;

    public Logger(bool verbose, string? logFile)
    {
        _verbose = verbose;
        if (logFile is not null)
        {
            _fileWriter = new StreamWriter(logFile, append: true) { AutoFlush = true };
        }
    }

    public void Info(string message) => Write(message);

    public void Verbose(string message)
    {
        if (_verbose)
        {
            Write(message);
        }
    }

    private void Write(string message)
    {
        var line = $"[{DateTimeOffset.Now:HH:mm:ss.fff}] {message}";
        lock (_lock)
        {
            Console.WriteLine(line);
            _fileWriter?.WriteLine(line);
        }
    }

    public void Dispose() => _fileWriter?.Dispose();
}
