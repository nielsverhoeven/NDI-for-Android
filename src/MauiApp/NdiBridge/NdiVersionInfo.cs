using NdiForAndroid.Services;

namespace NdiForAndroid.NdiBridge;

/// <summary>
/// Adapter exposing the native NDI runtime version to Core consumers (Settings About).
/// When the runtime is not yet initialized it performs a one-time availability probe:
/// <see cref="NdiRuntime.EnsureInitialized"/> paired immediately with
/// <see cref="NdiRuntime.ReleaseHandle"/> so the probe never pins a runtime handle.
/// The probe outcome (and the version string it observed) is cached — libndi
/// availability cannot change for the lifetime of the process.
/// </summary>
public sealed class NdiVersionInfo : INdiVersionInfo
{
    private readonly NdiRuntime _runtime;
    private readonly object _gate = new();

    private bool? _probedAvailable;
    private string? _probedVersion;

    public NdiVersionInfo(NdiRuntime runtime)
    {
        _runtime = runtime;
    }

    /// <inheritdoc />
    public bool IsRuntimeAvailable => _runtime.IsInitialized || TryProbe();

    /// <inheritdoc />
    public string? NativeVersion
    {
        get
        {
            if (!IsRuntimeAvailable)
                return null;

            // Prefer the live runtime value; fall back to the version captured during
            // the probe (the runtime may have been torn down again by a pending
            // discovery-server change after the probe handle was released).
            return _runtime.NativeVersion ?? _probedVersion;
        }
    }

    private bool TryProbe()
    {
        lock (_gate)
        {
            if (_probedAvailable is { } cached)
                return cached;

            var available = _runtime.EnsureInitialized();
            if (available)
            {
                _probedVersion = _runtime.NativeVersion;
                // Paired immediately — the probe must not keep the runtime pinned.
                _runtime.ReleaseHandle();
            }

            _probedAvailable = available;
            return available;
        }
    }
}
