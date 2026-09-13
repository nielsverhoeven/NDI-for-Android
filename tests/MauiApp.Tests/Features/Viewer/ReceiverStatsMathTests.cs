using NdiForAndroid.Features.Viewer;
using Xunit;

namespace NdiForAndroid.Tests.Features.Viewer;

public class ReceiverStatsMathTests
{
    [Fact]
    public void DropPercent_UsesTheIntervalDelta_NotTheLifetimeRatio()
    {
        // 10 000 clean frames earlier in the session, then a second in which 30 of 60 were dropped.
        // The lifetime ratio reads 30*100/10060 = 0.3%; the interval rate is 50%.
        Assert.Equal(50f, ReceiverStatsMath.DropPercent(totalDelta: 60, droppedDelta: 30));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(0, 5)]
    public void DropPercent_ZeroFrameInterval_ReturnsZero(long totalDelta, long droppedDelta)
    {
        Assert.Equal(0f, ReceiverStatsMath.DropPercent(totalDelta, droppedDelta));
    }

    [Fact]
    public void DropPercent_NoDrops_ReturnsZero()
    {
        Assert.Equal(0f, ReceiverStatsMath.DropPercent(totalDelta: 60, droppedDelta: 0));
    }

    [Theory]
    [InlineData(60, 60)]
    [InlineData(60, 90)]
    public void DropPercent_AllDropped_ReturnsHundred(long totalDelta, long droppedDelta)
    {
        Assert.Equal(100f, ReceiverStatsMath.DropPercent(totalDelta, droppedDelta));
    }

    [Theory]
    [InlineData(-5, -1)]
    [InlineData(-5, 3)]
    public void DropPercent_NegativeDeltas_ReturnZero(long totalDelta, long droppedDelta)
    {
        Assert.Equal(0f, ReceiverStatsMath.DropPercent(totalDelta, droppedDelta));
    }
}
