namespace NdiForAndroid.Services;

/// <summary>No-op dispatcher for non-Android build targets.</summary>
internal sealed class DefaultMainThreadDispatcher : IMainThreadDispatcher
{
    public void BeginInvokeOnMainThread(Action action) => action?.Invoke();
}
