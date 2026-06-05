using NdiForAndroid.Services;

namespace NdiForAndroid.Services;

public sealed class NoopScreenSharePlatformService : IScreenSharePlatformService
{
    public bool IsForegroundServiceActive { get; private set; }

    public Task StartForegroundSessionAsync(string streamName, CancellationToken cancellationToken = default)
    {
        IsForegroundServiceActive = true;
        return Task.CompletedTask;
    }

    public Task StopForegroundSessionAsync(CancellationToken cancellationToken = default)
    {
        IsForegroundServiceActive = false;
        return Task.CompletedTask;
    }
}
