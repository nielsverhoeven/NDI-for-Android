namespace NdiForAndroid.Services;

/// <summary>
/// A fake, controllable TimeProvider for unit testing.
/// </summary>
public class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _currentDateTimeOffset = DateTimeOffset.UnixEpoch;
    private long _highResolutionTicks = 0;
    private readonly List<Timer> _timers = new();

    public FakeTimeProvider(long initialSeconds = 0)
    {
        _currentDateTimeOffset = DateTimeOffset.UnixEpoch.AddSeconds(initialSeconds);
    }

    /// <summary>
    /// Advances time by the specified duration.
    /// </summary>
    public void Advance(TimeSpan duration)
    {
        _currentDateTimeOffset += duration;
        _highResolutionTicks += duration.Ticks;
    }

    /// <summary>
    /// Advances time by the specified number of seconds.
    /// </summary>
    public void AdvanceSeconds(double seconds) => Advance(TimeSpan.FromSeconds(seconds));

    public override DateTimeOffset GetUtcNow() => _currentDateTimeOffset;

    public override long GetTimestamp() => _highResolutionTicks;

    public new TimeSpan GetElapsedTime(long startTimestamp) =>
        TimeSpan.FromTicks(_highResolutionTicks - startTimestamp);

    public override Timer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new Timer(callback, state, dueTime, period);
        _timers.Add(timer);
        return timer;
    }
}
