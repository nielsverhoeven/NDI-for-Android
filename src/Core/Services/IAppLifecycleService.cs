namespace NdiForAndroid.Services;

public interface IAppLifecycleService
{
    bool IsInForeground { get; }
    bool IsLandscape { get; }
    DateTimeOffset? LastResumedAtUtc { get; }

    void NotifyResumed();
    void NotifyPaused();
    void NotifyConfigurationChanged(bool isLandscape);
}
