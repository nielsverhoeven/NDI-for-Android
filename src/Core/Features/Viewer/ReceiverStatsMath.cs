namespace NdiForAndroid.Features.Viewer;

/// <summary>
/// Pure arithmetic over the NDI receiver's performance counters, which are cumulative for the life
/// of the receiver — callers diff consecutive samples to get a rate.
/// </summary>
public static class ReceiverStatsMath
{
    /// <summary>
    /// Percentage of video frames dropped during one stats interval, given the deltas between this
    /// tick's cumulative counters and the previous tick's. Returns 0 for an interval that carried no
    /// frames at all, and clamps a negative delta (an unexpected counter reset) to a 0% result.
    /// </summary>
    public static float DropPercent(long totalDelta, long droppedDelta)
    {
        if (totalDelta <= 0 || droppedDelta <= 0)
            return 0f;

        if (droppedDelta >= totalDelta)
            return 100f;

        return (float)(droppedDelta * 100.0 / totalDelta);
    }
}
