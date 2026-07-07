namespace NdiForAndroid.Services;

/// <summary>
/// Read-only view of the native NDI runtime version, safe to consume from Core
/// ViewModels (no NDI SDK types cross this boundary). Implemented in the
/// MauiApp NDI bridge layer over the runtime lifecycle owner.
/// </summary>
public interface INdiVersionInfo
{
    /// <summary>Native NDI runtime version string, or null when the runtime is unavailable.</summary>
    string? NativeVersion { get; }

    /// <summary>False when libndi is not loadable for this ABI (e.g. x86 emulators).</summary>
    bool IsRuntimeAvailable { get; }
}
