using NdiForAndroid.NdiBridge;
using Xunit;

namespace NdiForAndroid.Tests.NdiBridge;

/// <summary>
/// Pins the receive-path latency budget applied by <c>NdiViewerBridge</c> to the receiver queue.
/// </summary>
public class ReceiveLatencyPolicyTests
{
    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    public void IsVideoFrameStale_OnlyWhenANewerFrameIsQueued(int queuedVideoFrames, bool expected)
    {
        Assert.Equal(expected, ReceiveLatencyPolicy.IsVideoFrameStale(queuedVideoFrames));
    }

    [Fact]
    public void QueuedAudioMs_ScalesWithFramesSamplesAndRate()
    {
        // 3 frames of 1600 samples at 48 kHz = 100 ms.
        Assert.Equal(100.0, ReceiveLatencyPolicy.QueuedAudioMs(3, 1600, 48_000), precision: 6);
    }

    [Theory]
    [InlineData(0, 1600, 48_000)]
    [InlineData(-1, 1600, 48_000)]
    [InlineData(2, 0, 48_000)]
    [InlineData(2, 1600, 0)]
    public void QueuedAudioMs_IsZeroForEmptyOrInvalidInput(int frames, int samples, int rate)
    {
        Assert.Equal(0.0, ReceiveLatencyPolicy.QueuedAudioMs(frames, samples, rate));
    }

    [Theory]
    // 1 x 33.3 ms queued: inside the jitter budget — play it.
    [InlineData(1, 1600, 48_000, false)]
    // 2 x 33.3 ms = 66.7 ms queued: above the budget — drop to catch up.
    [InlineData(2, 1600, 48_000, true)]
    // Small 10 ms frames: 5 queued (50 ms) is still within budget, 6 (60 ms) is not.
    [InlineData(5, 480, 48_000, false)]
    [InlineData(6, 480, 48_000, true)]
    // A large backlog after a Wi-Fi stall is always drained.
    [InlineData(20, 1602, 48_000, true)]
    public void ShouldDropAudioFrame_CapsTheBacklogAtTheJitterBudget(
        int queuedFrames, int samplesPerFrame, int sampleRate, bool expected)
    {
        Assert.Equal(expected, ReceiveLatencyPolicy.ShouldDropAudioFrame(queuedFrames, samplesPerFrame, sampleRate));
    }

    [Fact]
    public void AudioOutputBufferBytes_TargetsTheLowLatencyBuffer()
    {
        // 20 ms of 48 kHz stereo float = 960 frames x 2 ch x 4 bytes.
        Assert.Equal(7680, ReceiveLatencyPolicy.AudioOutputBufferBytes(48_000, 2, platformMinimumBytes: 0));
    }

    [Fact]
    public void AudioOutputBufferBytes_NeverGoesBelowThePlatformMinimum()
    {
        Assert.Equal(30_720, ReceiveLatencyPolicy.AudioOutputBufferBytes(48_000, 2, platformMinimumBytes: 30_720));
    }
}
