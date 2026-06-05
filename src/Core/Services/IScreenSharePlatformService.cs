namespace NdiForAndroid.Services;

public interface IScreenSharePlatformService
{
    bool IsForegroundServiceActive { get; }

    Task StartForegroundSessionAsync(string streamName, CancellationToken cancellationToken = default);
    Task StopForegroundSessionAsync(CancellationToken cancellationToken = default);
}
