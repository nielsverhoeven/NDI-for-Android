namespace NdiForAndroid.Services;

/// <summary>
/// Abstracts Android system-bar hiding and keep-screen-on for the full-screen viewer.
/// On non-Android platforms a no-op implementation is used.
/// </summary>
public interface IImmersiveModeService
{
    /// <summary>Hides system bars (status/navigation), swipe-to-reveal remains available.</summary>
    void EnterImmersive();

    /// <summary>Restores system bars.</summary>
    void ExitImmersive();

    /// <summary>Enables or disables keeping the screen on.</summary>
    void KeepScreenOn(bool enabled);
}
