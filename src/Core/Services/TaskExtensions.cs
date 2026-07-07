using System.Diagnostics;

namespace NdiForAndroid.Services;

/// <summary>
/// Helpers for intentionally unawaited (fire-and-forget) tasks so their exceptions
/// are always observed instead of being silently dropped or crashing the process.
/// </summary>
public static class TaskExtensions
{
    /// <summary>
    /// Observes the task's outcome without awaiting it. Any exception is logged
    /// (Debug output) and optionally forwarded to <paramref name="onError"/>.
    /// Use for non-critical housekeeping (state persistence, history recording).
    /// </summary>
    public static void FireAndForget(this Task task, Action<Exception>? onError = null)
    {
        _ = task.ContinueWith(
            t =>
            {
                var ex = t.Exception?.GetBaseException();
                if (ex is null)
                    return;

                Debug.WriteLine($"[FireAndForget] Unobserved task failure: {ex}");
                onError?.Invoke(ex);
            },
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }
}
