using NdiForAndroid.Services;

namespace NdiForAndroid.Services;

/// <summary>
/// No-op implementation of <see cref="IMulticastLockService"/> for non-Android build targets.
/// On non-Android platforms multicast is unrestricted so no lock acquisition is needed.
/// </summary>
public sealed class NoopMulticastLockService : IMulticastLockService
{
    public Task AcquireAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task ReleaseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
}
