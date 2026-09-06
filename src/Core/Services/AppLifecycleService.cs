namespace NdiForAndroid.Services;

public sealed class AppLifecycleService : IAppLifecycleService
{
    public bool IsInForeground { get; private set; }
    public bool IsLandscape { get; private set; }
    public double SmallestWidthDp { get; private set; }
    public DateTimeOffset? LastResumedAtUtc { get; private set; }

    public event Action? AppResumed;
    public event Action? AppPaused;
    public event Action<bool>? OrientationChanged;

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

    public void NotifyConfigurationChanged(bool isLandscape, double smallestWidthDp)
    {
        SmallestWidthDp = smallestWidthDp;
        if (IsLandscape == isLandscape) return;
        IsLandscape = isLandscape;
        OrientationChanged?.Invoke(isLandscape);
    }
}
