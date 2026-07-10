using Microsoft.Extensions.Logging;
using NdiForAndroid.Features.Settings.Repositories;
using NdiForAndroid.Features.Sources.Models;
using NdiForAndroid.Features.Sources.Repositories;

namespace NdiForAndroid.Services;

/// <summary>
/// Drives automatic NDI source discovery polling every ~5 seconds while the app is foregrounded.
/// Stops on background; debounces rapid manual triggers.
/// </summary>
public sealed class DiscoveryRefreshService : IDiscoveryRefreshService
{
    private static readonly TimeSpan DefaultPollingInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan DefaultDebounceWindow  = TimeSpan.FromSeconds(1);

    private readonly ISourceRepository _repository;
    private readonly ISettingsRepository _settingsRepository;
    private readonly ILogger<DiscoveryRefreshService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _pollingInterval;
    private readonly TimeSpan _debounceWindow;

    // 0 = stopped, 1 = running  — use Interlocked for atomic CAS (C2)
    private int _isRunning;
    // 0 = idle, 1 = in-flight
    private int _isInFlight;
    // 0 = discovery settings not yet applied this process, 1 = applied
    private int _settingsApplied;

    private CancellationTokenSource? _cts;
    private DateTimeOffset _lastCompletedAt = DateTimeOffset.MinValue;

    public event EventHandler<DiscoverySnapshot>? SnapshotReady;

    public DiscoveryRefreshService(
        ISourceRepository repository,
        ISettingsRepository settingsRepository,
        IAppLifecycleService lifecycle,
        ILogger<DiscoveryRefreshService> logger,
        TimeProvider? timeProvider = null,
        TimeSpan? pollingInterval = null,
        TimeSpan? debounceWindow = null)
    {
        _repository      = repository;
        _settingsRepository = settingsRepository;
        _logger          = logger;
        _timeProvider    = timeProvider ?? TimeProvider.System;
        _pollingInterval = pollingInterval ?? DefaultPollingInterval;
        _debounceWindow  = debounceWindow  ?? DefaultDebounceWindow;

        lifecycle.AppResumed += Start;
        lifecycle.AppPaused  += Stop;
    }

    /// <inheritdoc/>
    public void Start()
    {
        // Atomic compare-and-swap: only the first caller transitions 0→1 (C2)
        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
            return;

        _cts = new CancellationTokenSource();
        _ = Task.Run(() => PollLoopAsync(_cts.Token));
    }

    /// <inheritdoc/>
    public void Stop()
    {
        // Atomic compare-and-swap: only the caller that transitions 1→0 cancels (C2)
        if (Interlocked.CompareExchange(ref _isRunning, 0, 1) != 1)
            return;

        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    /// <inheritdoc/>
    public void RequestRefresh()
    {
        if (_isInFlight == 1)
            return;

        if (_timeProvider.GetUtcNow() - _lastCompletedAt < _debounceWindow)
            return;

        _ = Task.Run(() => ExecuteSinglePollAsync(CancellationToken.None));
    }

    /// <summary>
    /// Applies the persisted discovery settings (mDNS vs Discovery Server) to the NDI runtime
    /// exactly once per process, before the first discovery poll. Without this the runtime is
    /// only ever configured when the Settings page happens to load, so a configured discovery
    /// server is ignored on a normal launch and discovery silently falls back to mDNS.
    /// GetSettingsAsync applies the discovery orchestrator (and appearance) as a side effect.
    /// </summary>
    private async Task EnsureDiscoverySettingsAppliedAsync()
    {
        if (Interlocked.CompareExchange(ref _settingsApplied, 1, 0) != 0)
            return;

        try
        {
            await _settingsRepository.GetSettingsAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Non-fatal: discovery proceeds with whatever mode the runtime already has.
            _logger.LogWarning(ex, "Applying persisted discovery settings before first poll failed");
            Interlocked.Exchange(ref _settingsApplied, 0); // allow a later retry
        }
    }

    private async Task PollLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await ExecuteSinglePollAsync(ct).ConfigureAwait(false);

            try
            {
                await Task.Delay(_pollingInterval, _timeProvider, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task ExecuteSinglePollAsync(CancellationToken ct)
    {
        // At-most-one-in-flight guard
        if (Interlocked.CompareExchange(ref _isInFlight, 1, 0) != 0)
            return;

        try
        {
            await EnsureDiscoverySettingsAppliedAsync().ConfigureAwait(false);
            var snapshot = await _repository.DiscoverAsync(ct).ConfigureAwait(false);
            _lastCompletedAt = _timeProvider.GetUtcNow();

            // Only raise if still running (guards against race after Stop)
            if (_isRunning == 1 || ct == CancellationToken.None)
                SnapshotReady?.Invoke(this, snapshot);
        }
        catch (OperationCanceledException)
        {
            // Normal stop — swallow silently
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Discovery poll failed");

            var failureSnapshot = new DiscoverySnapshot(
                SnapshotId: Guid.NewGuid().ToString(),
                Status: DiscoveryStatus.Failure,
                Sources: Array.Empty<NdiForAndroid.Features.Sources.Models.NdiSource>(),
                CompletedAtEpochMillis: _timeProvider.GetUtcNow().ToUnixTimeMilliseconds(),
                ErrorMessage: ex.Message);

            SnapshotReady?.Invoke(this, failureSnapshot);
        }
        finally
        {
            Interlocked.Exchange(ref _isInFlight, 0);
        }
    }
}
