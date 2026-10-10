using NdiForAndroid.Features.Viewer;
using Xunit;

namespace NdiForAndroid.Tests.Features.Viewer;

public class ReceiverStatsMathTests
{
    [Fact]
    public void DropPercent_UsesTheIntervalDelta_NotTheLifetimeRatio()
    {
        // 10 000 clean frames earlier in the session, then a second in which 60 were delivered and
        // 30 more were dropped (90 received). The lifetime ratio reads 30*100/10090 = 0.3%; the
        // interval rate is 30/90 = 33.33%.
        Assert.Equal(33.33f, ReceiverStatsMath.DropPercent(totalDelta: 60, droppedDelta: 30), 2);
    }

    [Fact]
    public void DropPercent_ZeroFrameInterval_ReturnsZero()
    {
        Assert.Equal(0f, ReceiverStatsMath.DropPercent(totalDelta: 0, droppedDelta: 0));
    }

    [Fact]
    public void DropPercent_NoDrops_ReturnsZero()
    {
        Assert.Equal(0f, ReceiverStatsMath.DropPercent(totalDelta: 60, droppedDelta: 0));
    }

    [Fact]
    public void DropPercent_NothingDeliveredButFramesDropped_ReturnsHundred()
    {
        // The SDK received 60 frames and discarded every one: the worst second a receiver can have.
        // Under the old subset formula this read 0 %.
        Assert.Equal(100f, ReceiverStatsMath.DropPercent(totalDelta: 0, droppedDelta: 60));
    }

    [Fact]
    public void DropPercent_HalfDropped_ReturnsFifty()
    {
        // 50 delivered + 50 dropped = 100 received, half of them discarded (the old clamp said 100).
        Assert.Equal(50f, ReceiverStatsMath.DropPercent(totalDelta: 50, droppedDelta: 50));
    }

    [Fact]
    public void DropPercent_MoreDroppedThanDelivered_IsNotClamped()
    {
        // 60 delivered + 90 dropped = 150 received; a legal state, not 100 %.
        Assert.Equal(60f, ReceiverStatsMath.DropPercent(totalDelta: 60, droppedDelta: 90));
    }

    [Fact]
    public void DropPercent_MeasuredTabletCase_1080p60_ReadsTheTrueShare()
    {
        // Device 2026-10-10, Balanced 1080p at 194 Mbit/s: fps 49.9, dTotal 49.7, dDropped 10.7,
        // sender 59.4 fps — dTotal+dDropped = 60.3 tracks the sender. Rounded: 11/(50+11) = 18.03 %
        // (the old formula printed 21.5 %).
        Assert.Equal(18.03f, ReceiverStatsMath.DropPercent(totalDelta: 50, droppedDelta: 11), 2);
    }

    [Fact]
    public void DropPercent_SmallestNonTrivialInterval()
    {
        Assert.Equal(50f, ReceiverStatsMath.DropPercent(totalDelta: 1, droppedDelta: 1));
    }

    [Theory]
    [InlineData(-5, -1)]
    [InlineData(-5, 3)]
    [InlineData(5, -1)]
    public void DropPercent_NegativeDeltas_ReturnZero(long totalDelta, long droppedDelta)
    {
        // A negative delta in either counter means the receiver was recreated under us; the pair is
        // not comparable.
        Assert.Equal(0f, ReceiverStatsMath.DropPercent(totalDelta, droppedDelta));
    }

    [Fact]
    public void DropPercent_HugeCounters_DoNotOverflow()
    {
        Assert.Equal(50f, ReceiverStatsMath.DropPercent(long.MaxValue / 2, long.MaxValue / 2));
    }
}
