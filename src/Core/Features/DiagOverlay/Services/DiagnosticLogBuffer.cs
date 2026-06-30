using System.Text.RegularExpressions;

namespace NdiForAndroid.Features.DiagOverlay.Services;

/// <summary>
/// In-memory ring buffer for diagnostic log entries with IP redaction.
/// </summary>
public sealed class DiagnosticLogBuffer
{
    private readonly object _lock = new();
    private readonly Queue<LogEntry> _entries = new(200);
    private const int MaxCapacity = 200;

    public IReadOnlyList<LogEntry> GetEntries(int count = 200)
    {
        lock (_lock)
        {
            var result = _entries.Take(count).ToList();
            return result.AsReadOnly();
        }
    }

    public void Add(string category, string message, LogLevel level = LogLevel.Info)
    {
        var entry = new LogEntry(
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            RedactSensitiveData(category),
            RedactSensitiveData(message),
            level);

        lock (_lock)
        {
            _entries.Enqueue(entry);
            while (_entries.Count > MaxCapacity)
                _entries.Dequeue();
        }
    }

    public void Clear()
    {
        lock (_lock)
            _entries.Clear();
    }

    /// <summary>Redacts IP addresses and sensitive NDI data from log strings.</summary>
    private static string RedactSensitiveData(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        // Redact IPv4 addresses: x.x.x.x where x is 1-3 digits
        var redacted = Regex.Replace(input, @"\b\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\b", "***.***.***.***/port");

        // Redact IPv6 addresses (simplified)
        redacted = Regex.Replace(redacted, @"\b[0-9a-fA-F:]+:[0-9a-fA-F:]+\b", "[ipv6-redacted]");

        return redacted;
    }

    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
    }

    public record LogEntry(
        long TimestampEpochMillis,
        string Category,
        string Message,
        LogLevel Level);
}
