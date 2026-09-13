using System.Text.Json;
using System.Text.Json.Serialization;

namespace NdiForAndroid.UITests.Infrastructure;

/// <summary>One entry from <c>quarantine.json</c>: a known-flaky test, its owner and its expiry.</summary>
public sealed record QuarantineEntry(
    [property: JsonPropertyName("test")] string Test,
    [property: JsonPropertyName("owner")] string Owner,
    [property: JsonPropertyName("expires")] string Expires,
    [property: JsonPropertyName("reason")] string Reason)
{
    public DateTime ExpiresUtc => DateTime.Parse(
        Expires, null, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal);
}

/// <summary>
/// Converts a known-flaky test's failure into a reported, non-blocking skip until its entry in
/// <c>quarantine.json</c> expires, after which the same failure blocks the pipeline again.
/// </summary>
public static class Quarantine
{
    private static readonly Lazy<IReadOnlyList<QuarantineEntry>> Entries = new(Load);

    /// <summary>
    /// Null when <paramref name="fullyQualifiedTestName"/> is not quarantined, or its entry has
    /// expired. Otherwise the message to report the test as skipped with.
    /// </summary>
    public static string? ActiveReasonFor(string fullyQualifiedTestName)
    {
        var entry = Entries.Value.FirstOrDefault(e =>
            string.Equals(e.Test, fullyQualifiedTestName, StringComparison.Ordinal));

        if (entry is null)
            return null;

        if (entry.ExpiresUtc <= DateTime.UtcNow)
        {
            Console.Error.WriteLine(
                $"[quarantine] '{fullyQualifiedTestName}' expired {entry.Expires} (owner {entry.Owner}) " +
                "— treating this failure as real. Renew or remove the quarantine.json entry.");
            return null;
        }

        return $"QUARANTINED (owner: {entry.Owner}, expires: {entry.Expires}) — {entry.Reason}";
    }

    private static IReadOnlyList<QuarantineEntry> Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "quarantine.json");

        try
        {
            if (!File.Exists(path))
                return [];

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<QuarantineEntry>>(json) ?? [];
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[quarantine] could not read '{path}': {ex.Message}");
            return [];
        }
    }
}
