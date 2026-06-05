using NdiForAndroid.Services;

namespace NdiForAndroid.Services;

public sealed class AppLifecycleService : IAppLifecycleService
{
    public bool IsInForeground { get; private set; }
    public bool IsLandscape { get; private set; }
    public DateTimeOffset? LastResumedAtUtc { get; private set; }

    public void NotifyResumed()
    {
        IsInForeground = true;
        LastResumedAtUtc = DateTimeOffset.UtcNow;
    }

    public void NotifyPaused()
    {
        IsInForeground = false;
    }

    public void NotifyConfigurationChanged(bool isLandscape)
    {
        IsLandscape = isLandscape;
    }
}
