namespace NdiForAndroid.Features.Viewer;

/// <summary>
/// Pure arithmetic over the NDI receiver's performance counters, which are cumulative for the life
/// of the receiver — callers diff consecutive samples to get a rate.
/// </summary>
public static class ReceiverStatsMath
{
    /// <summary>
    /// Percentage of the video frames the receiver got in one stats interval that it discarded
    /// because they could not be de-queued in time, given the deltas between this tick's cumulative
    /// counters and the previous tick's. The SDK's two counters are DISJOINT (settled on device
    /// 2026-10-10: total+dropped tracks the sender's frame rate in every configuration while total
    /// tracks the app's fps): total = frames handed to the app, dropped = frames the SDK discarded
    /// before delivery. So the denominator is total + dropped (frames received), and the result is
    /// bounded [0, 100] by construction — exactly 100 when frames arrived and none was delivered.
    /// Returns 0 for an interval that received no frames at all (nothing to judge; the hint's fps
    /// arm names a silent source), and 0 for a negative delta (an unexpected counter reset) in
    /// either counter.
    /// </summary>
    public static float DropPercent(long totalDelta, long droppedDelta)
    {
        if (totalDelta < 0 || droppedDelta <= 0)
            return 0f;

        return (float)(droppedDelta * 100.0 / ((double)totalDelta + droppedDelta));
    }
}
