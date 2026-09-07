using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using NdiForAndroid.NdiBridge;

namespace NdiForAndroid.Features.Viewer.ViewModels;

public partial class ViewerViewModel
{
    /// <summary>The bridge refreshes fps / dropped % once per second (NdiViewerBridge.StatsIntervalMs), so sample at the same rate.</summary>
    private static readonly TimeSpan StatsSampleInterval = TimeSpan.FromSeconds(1);

    private ITimer? _statsTimer;
    private ConnectionHintPolicy.State _hintState = ConnectionHintPolicy.State.Idle;

    /// <summary>
    /// "Connection weak — try Smooth" while the bridge reports sustained low fps / high drops;
    /// null otherwise. Purely advisory — nothing here ever calls SetQualityProfile (#331).
    /// </summary>
    [ObservableProperty]
    private string? _connectionHint;

    private void StartStatsWatchdog()
    {
        ResetConnectionHint();
        if (_statsTimer is null)
            _statsTimer = _timeProvider.CreateTimer(_ => SampleConnectionStats(), null, StatsSampleInterval, StatsSampleInterval);
        else
            _statsTimer.Change(StatsSampleInterval, StatsSampleInterval);
    }

    private void StopStatsWatchdog()
    {
        _statsTimer?.Change(Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        ResetConnectionHint();
    }

    /// <summary>Timer callback (thread-pool thread): read the bridge's lock-free counters here, mutate observable state on the UI thread.</summary>
    private void SampleConnectionStats()
    {
        try
        {
            var connected = _bridge.GetConnectionState() == ConnectionState.Connected;
            var fps = _bridge.GetMeasuredFps();
            var dropPercent = _bridge.GetDroppedFramePercent();
            _dispatcher.BeginInvokeOnMainThread(() => ApplyConnectionSample(connected, fps, dropPercent));
        }
        catch
        {
            // A timer callback must never take down the process; a missed sample is harmless.
        }
    }

    /// <summary>Applies one 1 s stats sample. Public so tests (and any host) can push samples directly. Main thread only.</summary>
    public void ApplyConnectionSample(bool connected, float fps, float dropPercent)
    {
        // Not a judgement about the link while (re)connecting or when nothing is being received
        // (also keeps the hint off the x86 emulator, where the bridge never reports Connected).
        if (!IsPlaying || IsReconnecting || !connected)
        {
            ResetConnectionHint();
            return;
        }

        _hintState = ConnectionHintPolicy.Next(_hintState, fps, dropPercent);
        ConnectionHint = ConnectionHintPolicy.HintText(_hintState.IsHintActive, QualityProfile);
    }

    private void ResetConnectionHint()
    {
        _hintState = ConnectionHintPolicy.State.Idle;
        ConnectionHint = null;
    }
}
