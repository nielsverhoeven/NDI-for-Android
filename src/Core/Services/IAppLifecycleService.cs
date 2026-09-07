namespace NdiForAndroid.Services;

public interface IAppLifecycleService
{
    bool IsInForeground { get; }
    bool IsLandscape { get; }

    /// <summary>Shortest screen edge in device-independent units, as last reported by the platform. 0 until the first configuration report.</summary>
    double SmallestWidthDp { get; }

    DateTimeOffset? LastResumedAtUtc { get; }

    /// <summary>Raised synchronously inside <see cref="NotifyResumed"/> after state is updated.</summary>
    event Action? AppResumed;

    /// <summary>Raised synchronously inside <see cref="NotifyPaused"/> after state is updated.</summary>
    event Action? AppPaused;

    /// <summary>Raised only when the orientation actually changes, after <see cref="IsLandscape"/> and <see cref="SmallestWidthDp"/> are updated.</summary>
    event Action<bool>? OrientationChanged;

    void NotifyResumed();
    void NotifyPaused();
    void NotifyConfigurationChanged(bool isLandscape, double smallestWidthDp);
}
