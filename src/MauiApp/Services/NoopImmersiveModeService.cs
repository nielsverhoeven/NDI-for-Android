namespace NdiForAndroid.Services;

/// <summary>
/// No-op implementation of <see cref="IImmersiveModeService"/> for non-Android build targets.
/// </summary>
public sealed class NoopImmersiveModeService : IImmersiveModeService
{
    public void EnterImmersive() { }

    public void ExitImmersive() { }

    public void KeepScreenOn(bool enabled) { }
}
