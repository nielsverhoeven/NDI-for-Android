namespace NdiForAndroid.Services;

public sealed class AppLifecycleService : IAppLifecycleService
{
    public bool IsInForeground { get; private set; }
    public bool IsLandscape { get; private set; }
    public DateTimeOffset? LastResumedAtUtc { get; private set; }

    public event Action? AppResumed;
    public event Action? AppPaused;

    public void NotifyResumed()
    {
        IsInForeground = true;
        LastResumedAtUtc = DateTimeOffset.UtcNow;
        AppResumed?.Invoke();
    }

    public void NotifyPaused()
    {
        IsInForeground = false;
        AppPaused?.Invoke();
    }

    public void NotifyConfigurationChanged(bool isLandscape)
    {
        IsLandscape = isLandscape;
    }
}
