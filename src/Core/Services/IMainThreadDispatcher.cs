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
