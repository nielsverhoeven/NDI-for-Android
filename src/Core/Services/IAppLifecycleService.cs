namespace NdiForAndroid.Services;

public interface IAppLifecycleService
{
    bool IsInForeground { get; }
    bool IsLandscape { get; }
    DateTimeOffset? LastResumedAtUtc { get; }

    /// <summary>Raised synchronously inside <see cref="NotifyResumed"/> after state is updated.</summary>
    event Action? AppResumed;

    /// <summary>Raised synchronously inside <see cref="NotifyPaused"/> after state is updated.</summary>
    event Action? AppPaused;

    void NotifyResumed();
    void NotifyPaused();
    void NotifyConfigurationChanged(bool isLandscape);
}
