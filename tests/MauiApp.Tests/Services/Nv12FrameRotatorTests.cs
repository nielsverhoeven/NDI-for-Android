using NdiForAndroid.Services;
using Xunit;

namespace NdiForAndroid.Tests.Services;

public class Nv12FrameRotatorTests
{
    // 4x2 frame: Y = 0..7 row-major, chroma 2x1 pairs (U,V) = (10,11),(12,13).
    private static readonly byte[] Source = { 0, 1, 2, 3, 4, 5, 6, 7, 10, 11, 12, 13 };

    [Fact]
    public void Rotate_90_ProducesClockwiseRotatedPortraitFrame()
    {
        var dst = new byte[Nv12FrameRotator.RequiredLength(4, 2)];
        var size = Nv12FrameRotator.Rotate(Source, 4, 2, 90, dst);
        Assert.Equal((2, 4), size);
        Assert.Equal(new byte[] { 4, 0, 5, 1, 6, 2, 7, 3 }, dst[..8]);     // Y: columns bottom-up become rows
        Assert.Equal(new byte[] { 10, 11, 12, 13 }, dst[8..]);              // UV pairs stay U-then-V
    }

    [Fact]
    public void Rotate_180_ReversesRowsAndColumns()
    {
        var dst = new byte[12];
        var size = Nv12FrameRotator.Rotate(Source, 4, 2, 180, dst);
        Assert.Equal((4, 2), size);
        Assert.Equal(new byte[] { 7, 6, 5, 4, 3, 2, 1, 0 }, dst[..8]);
        Assert.Equal(new byte[] { 12, 13, 10, 11 }, dst[8..]);
    }

    [Fact]
    public void Rotate_270_ProducesCounterClockwiseRotatedFrame()
    {
        var dst = new byte[12];
        var size = Nv12FrameRotator.Rotate(Source, 4, 2, 270, dst);
        Assert.Equal((2, 4), size);
        Assert.Equal(new byte[] { 3, 7, 2, 6, 1, 5, 0, 4 }, dst[..8]);
        Assert.Equal(new byte[] { 12, 13, 10, 11 }, dst[8..]);
    }

    [Fact]
    public void Rotate_0_CopiesUnchanged()
    {
        var dst = new byte[12];
        var size = Nv12FrameRotator.Rotate(Source, 4, 2, 0, dst);
        Assert.Equal((4, 2), size);
        Assert.Equal(Source, dst);
    }

    [Fact]
    public void Rotate_90_FourTimes_RoundTrips()
    {
        var a = (byte[])Source.Clone(); var b = new byte[12];
        int w = 4, h = 2;
        for (var i = 0; i < 4; i++)
        {
            (w, h) = Nv12FrameRotator.Rotate(a, w, h, 90, b);
            (a, b) = (b, a);
        }
        Assert.Equal((4, 2), (w, h));
        Assert.Equal(Source, a);
    }

    [Fact]
    public void Rotate_90_Then_270_RoundTrips()
    {
        var mid = new byte[12]; var back = new byte[12];
        var (w, h) = Nv12FrameRotator.Rotate(Source, 4, 2, 90, mid);
        Nv12FrameRotator.Rotate(mid, w, h, 270, back);
        Assert.Equal(Source, back);
    }

    [Fact]
    public void Rotate_LargeFrame_90_MatchesReferenceMapping()
    {
        const int w = 64, h = 48;
        var src = new byte[Nv12FrameRotator.RequiredLength(w, h)];
        new Random(1).NextBytes(src);
        var dst = new byte[src.Length];
        var (dw, dh) = Nv12FrameRotator.Rotate(src, w, h, 90, dst);
        Assert.Equal((h, w), (dw, dh));
        for (var y = 0; y < h; y++)
            for (var x = 0; x < w; x++)
                Assert.Equal(src[y * w + x], dst[x * dw + (h - 1 - y)]);   // luma: dst(h-1-y, x) = src(x, y)
        for (var cy = 0; cy < h / 2; cy++)
            for (var cx = 0; cx < w / 2; cx++)
            {
                var s = w * h + (cy * (w / 2) + cx) * 2;
                var d = dw * dh + (cx * (dw / 2) + (h / 2 - 1 - cy)) * 2;
                Assert.Equal(src[s], dst[d]);
                Assert.Equal(src[s + 1], dst[d + 1]);
            }
    }

    [Theory]
    [InlineData(3, 2)]
    [InlineData(4, 3)]
    [InlineData(0, 2)]
    public void Rotate_OddOrEmptyDimensions_Throws(int w, int h) =>
        Assert.Throws<ArgumentException>(() => Nv12FrameRotator.Rotate(new byte[64], w, h, 90, new byte[64]));

    [Fact]
    public void Rotate_UnsupportedAngle_Throws() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => Nv12FrameRotator.Rotate(Source, 4, 2, 45, new byte[12]));

    [Fact]
    public void Rotate_DestinationTooSmall_Throws() =>
        Assert.Throws<ArgumentException>(() => Nv12FrameRotator.Rotate(Source, 4, 2, 90, new byte[11]));

    [Theory]
    [InlineData(1280, 720, 90, 720, 1280)]
    [InlineData(1280, 720, 270, 720, 1280)]
    [InlineData(1280, 720, 180, 1280, 720)]
    [InlineData(1280, 720, 0, 1280, 720)]
    public void RotatedSize_SwapsForQuarterTurns(int w, int h, int deg, int ew, int eh) =>
        Assert.Equal((ew, eh), Nv12FrameRotator.RotatedSize(w, h, deg));

    [Fact]
    public void Rotate_720p_90_StaysFarBelowTheFrameBudget()
    {
        var src = new byte[Nv12FrameRotator.RequiredLength(1280, 720)];
        var dst = new byte[src.Length];
        Nv12FrameRotator.Rotate(src, 1280, 720, 90, dst); // warm-up
        var sw = System.Diagnostics.Stopwatch.StartNew();
        for (var i = 0; i < 30; i++) Nv12FrameRotator.Rotate(src, 1280, 720, 90, dst);
        sw.Stop();
        Assert.True(sw.Elapsed.TotalMilliseconds / 30 < 100, $"avg {sw.Elapsed.TotalMilliseconds / 30:F1} ms per 720p rotate"); // generous CI bound; measured ~1.2 ms
    }
}
