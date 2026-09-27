namespace NdiForAndroid.Services;

/// <summary>Abstraction for dispatching work onto the main (UI) thread.</summary>
public interface IMainThreadDispatcher
{
    void BeginInvokeOnMainThread(Action action);
}

/// <summary>
/// A fake, synchronous dispatcher for unit testing.
/// Invokes actions immediately and on the current thread.
/// </summary>
public class FakeMainThreadDispatcher : IMainThreadDispatcher
{
    public void BeginInvokeOnMainThread(Action action) => action?.Invoke();
}

/// <summary>
/// A queued dispatcher for unit tests that depend on the order in which work reaches the UI thread
/// (#409). <see cref="FakeMainThreadDispatcher"/> runs every post inline, so two posts made from
/// different background callbacks — a timer tick and a bridge event, say — collapse into one turn
/// and can never be observed in the other order. This fake models the looper the way MAUI's
/// <c>MainThread.BeginInvokeOnMainThread</c> uses it: a post made off the UI thread is queued (FIFO)
/// until <see cref="Drain"/> runs it; a post made on the UI thread — from inside an action
/// <see cref="Drain"/> is running — runs immediately, in place. The thread calling
/// <see cref="Drain"/> is the UI thread for as long as the drain lasts, and only then.
/// </summary>
public sealed class QueuedMainThreadDispatcher : IMainThreadDispatcher
{
    private readonly Queue<Action> _queue = new();
    private readonly object _gate = new();

    // Managed id of the thread currently draining; 0 while nobody is (managed ids start at 1).
    private int _uiThreadId;

    /// <summary>Number of posts waiting for the next <see cref="Drain"/>.</summary>
    public int PendingCount
    {
        get
        {
            lock (_gate)
                return _queue.Count;
        }
    }

    public void BeginInvokeOnMainThread(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (Volatile.Read(ref _uiThreadId) == Environment.CurrentManagedThreadId)
        {
            action();
            return;
        }

        lock (_gate)
            _queue.Enqueue(action);
    }

    /// <summary>
    /// Runs the queued posts in FIFO order on the calling thread until the queue is empty, including
    /// posts other threads queue while it runs, and returns how many ran. An exception from a post
    /// propagates and leaves the posts behind it queued. Not re-entrant: a looper does not pump
    /// itself from inside a message.
    /// </summary>
    public int Drain()
    {
        var thisThread = Environment.CurrentManagedThreadId;
        if (Interlocked.CompareExchange(ref _uiThreadId, thisThread, 0) != 0)
            throw new InvalidOperationException("Drain() is already running.");

        try
        {
            var ran = 0;
            while (true)
            {
                Action next;
                lock (_gate)
                {
                    if (_queue.Count == 0)
                        return ran;

                    next = _queue.Dequeue();
                }

                next();
                ran++;
            }
        }
        finally
        {
            Volatile.Write(ref _uiThreadId, 0);
        }
    }
}
