namespace NdiForAndroid.Services;

public interface IScreenSharePlatformService
{
    bool IsForegroundServiceActive { get; }

    Task StartForegroundSessionAsync(string streamName, VideoInputKind kind, CancellationToken cancellationToken = default);
    Task StopForegroundSessionAsync(CancellationToken cancellationToken = default);
}
