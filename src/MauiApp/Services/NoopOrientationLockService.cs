namespace NdiForAndroid.Services;

/// <summary>
/// No-op implementation of <see cref="IOrientationLockService"/> for non-Android build targets.
/// </summary>
public sealed class NoopOrientationLockService : IOrientationLockService
{
    public void RequestLandscape() { }

    public void RequestPortrait() { }

    public void Release() { }
}
