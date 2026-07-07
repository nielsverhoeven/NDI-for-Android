using System.Runtime.InteropServices;
using System.Text.Json;
using NdiForAndroid.NdiBridge.Interop;
using NdiForAndroid.Services;

namespace NdiForAndroid.NdiBridge;

/// <summary>
/// Owns the NDI library lifecycle: platform prerequisites (NsdManager), the
/// ndi-config.v1.json discovery-server configuration, NDIlib_initialize, and
/// version reporting. All bridges must go through <see cref="EnsureInitialized"/>
/// before creating native objects, and register active handles so discovery-server
/// changes only reinitialize the library when nothing is live.
/// </summary>
public sealed class NdiRuntime
{
    private readonly INdiPlatformBootstrap _bootstrap;
    private readonly object _gate = new();

    private bool _initialized;
    private string? _nativeVersion;
    private string _appliedDiscoveryServers = string.Empty;
    private string _pendingDiscoveryServers = string.Empty;
    private int _activeHandles;

    public NdiRuntime(INdiPlatformBootstrap bootstrap)
    {
        _bootstrap = bootstrap;
    }

    /// <summary>Native library version string (e.g. "NDI SDK ANDROID … 6.3.1.0"), null until initialized.</summary>
    public string? NativeVersion
    {
        get { lock (_gate) return _nativeVersion; }
    }

    public bool IsInitialized
    {
        get { lock (_gate) return _initialized; }
    }

    /// <summary>
    /// Sets the discovery server list ("host:port,host2:port2" — empty for pure mDNS).
    /// The NDI library reads its config at initialize time, so a change is applied
    /// immediately when idle, or deferred until the last active handle is released.
    /// </summary>
    public void SetDiscoveryServers(string commaSeparatedServers)
    {
        lock (_gate)
        {
            _pendingDiscoveryServers = commaSeparatedServers ?? string.Empty;
            if (_initialized && _pendingDiscoveryServers != _appliedDiscoveryServers && _activeHandles == 0)
                ShutdownLocked();
        }
    }

    /// <summary>
    /// Ensures the native library is initialized with the current discovery config
    /// and registers one active handle. Pair every successful call with
    /// <see cref="ReleaseHandle"/>. Returns false when the CPU is unsupported or
    /// init failed (callers must degrade gracefully, never crash).
    /// </summary>
    public bool EnsureInitialized()
    {
        lock (_gate)
        {
            if (!_initialized)
            {
                _bootstrap.EnsureReady();
                WriteConfigFileLocked(_pendingDiscoveryServers);

                try
                {
                    if (!NdiNativeMethods.NDIlib_initialize())
                        return false;
                }
                catch (DllNotFoundException)
                {
                    // libndi.so ships arm64-v8a/armeabi-v7a only — on x86/x86_64
                    // (emulators, some Chromebooks) the app must run with NDI
                    // features disabled instead of crashing.
                    return false;
                }
                catch (EntryPointNotFoundException)
                {
                    return false;
                }

                _appliedDiscoveryServers = _pendingDiscoveryServers;
                _nativeVersion = Marshal.PtrToStringAnsi(NdiNativeMethods.NDIlib_version());
                _initialized = true;
            }

            _activeHandles++;
            return true;
        }
    }

    /// <summary>
    /// Releases an active handle. If a discovery-server change is pending and this
    /// was the last handle, the library is torn down so the next
    /// <see cref="EnsureInitialized"/> picks up the new config.
    /// </summary>
    public void ReleaseHandle()
    {
        lock (_gate)
        {
            _activeHandles = Math.Max(0, _activeHandles - 1);
            if (_activeHandles == 0 && _initialized && _pendingDiscoveryServers != _appliedDiscoveryServers)
                ShutdownLocked();
        }
    }

    private void ShutdownLocked()
    {
        NdiNativeMethods.NDIlib_destroy();
        _initialized = false;
        _nativeVersion = null;
    }

    /// <summary>
    /// Writes ndi-config.v1.json into app data and points NDI_CONFIG_DIR at it.
    /// Must happen before NDIlib_initialize — the library reads config exactly once.
    /// </summary>
    private static void WriteConfigFileLocked(string discoveryServers)
    {
        try
        {
            var configDir = FileSystem.AppDataDirectory;
            var configPath = Path.Combine(configDir, "ndi-config.v1.json");

            var json = JsonSerializer.Serialize(new
            {
                ndi = new
                {
                    networks = new { discovery = discoveryServers },
                    // Multicast sending stays off: unreliable on Wi-Fi (NDI perf docs).
                    multicast = new { send = new { enable = false } },
                },
            });

            File.WriteAllText(configPath, json);
            Environment.SetEnvironmentVariable("NDI_CONFIG_DIR", configDir);
        }
        catch
        {
            // Non-fatal: without config the library falls back to pure mDNS discovery.
        }
    }
}
