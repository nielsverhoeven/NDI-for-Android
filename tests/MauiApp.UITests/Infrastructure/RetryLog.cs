using System.Text.Json;

namespace NdiForAndroid.UITests.Infrastructure;

/// <summary>Append-only record of every retry attempt, for the nightly flake aggregation.</summary>
public static class RetryLog
{
    private static readonly object Gate = new();

    public static string LogPath
    {
        get
        {
            var evidenceDir = FailureEvidence.ArtifactDirectory.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var parent = Path.GetDirectoryName(evidenceDir) ?? evidenceDir;
            return Path.Combine(parent, "retry-log.ndjson");
        }
    }

    public static void Append(string testName, int attempt, int totalAttempts, bool passed)
    {
        try
        {
            var line = JsonSerializer.Serialize(new
            {
                test = testName,
                attempt,
                totalAttempts,
                passed,
                utc = DateTime.UtcNow,
            });

            var path = LogPath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            lock (Gate)
            {
                File.AppendAllText(path, line + Environment.NewLine);
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[RetryLog] could not append: {ex.Message}");
        }
    }
}
