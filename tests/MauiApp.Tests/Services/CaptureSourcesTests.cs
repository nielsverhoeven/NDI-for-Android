using NdiForAndroid.Services;
using Xunit;

namespace NdiForAndroid.Tests.Services;

public class CaptureSourcesTests
{
    private sealed class FakeVideoCaptureSource : IVideoCaptureSource
    {
        public event EventHandler<CapturedVideoFrame>? FrameReady { add { } remove { } }

        public event EventHandler<CaptureStoppedEventArgs>? Stopped;

        public bool IsActive { get; private set; }

        public Task StartAsync(VideoInputKind kind, CancellationToken cancellationToken = default)
        {
            IsActive = true;
            return Task.CompletedTask;
        }

        public Task StopAsync()
        {
            IsActive = false;
            return Task.CompletedTask;
        }

        public void RaiseStopped(CaptureStopReason reason, string? message = null) =>
            Stopped?.Invoke(this, new CaptureStoppedEventArgs(reason, message));
    }

    [Fact]
    public void Stopped_WhenRaised_CarriesReasonAndMessage()
    {
        var source = new FakeVideoCaptureSource();
        CaptureStoppedEventArgs? received = null;
        source.Stopped += (_, e) => received = e;

        source.RaiseStopped(CaptureStopReason.ProjectionStopped, "consent revoked");

        Assert.NotNull(received);
        Assert.Equal(CaptureStopReason.ProjectionStopped, received!.Reason);
        Assert.Equal("consent revoked", received.Message);
    }

    [Fact]
    public void CaptureStoppedEventArgs_MessageIsOptional()
    {
        var args = new CaptureStoppedEventArgs(CaptureStopReason.DeviceError);

        Assert.Null(args.Message);
    }

    [Theory]
    [InlineData(CaptureStopReason.ProjectionStopped)]
    [InlineData(CaptureStopReason.CameraDisconnected)]
    [InlineData(CaptureStopReason.CameraError)]
    [InlineData(CaptureStopReason.DeviceError)]
    public void AllReasons_ConstructEventArgsWithoutThrowing(CaptureStopReason reason)
    {
        var args = new CaptureStoppedEventArgs(reason);

        Assert.Equal(reason, args.Reason);
    }
}
