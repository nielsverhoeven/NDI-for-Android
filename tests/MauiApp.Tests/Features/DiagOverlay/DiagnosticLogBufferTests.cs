using Microsoft.Extensions.Time.Testing;
using NdiForAndroid.Features.DiagOverlay.Services;
using Xunit;

namespace NdiForAndroid.Tests.Features.DiagOverlay;

public class DiagnosticLogBufferTests
{
    [Fact]
    public void Add_StampsEntryWithInjectedClock()
    {
        var now = new DateTimeOffset(2026, 9, 7, 10, 0, 0, TimeSpan.Zero);
        var clock = new FakeTimeProvider(now);
        var sut = new DiagnosticLogBuffer(clock);

        sut.Add("Cat", "msg");

        Assert.Equal(now.ToUnixTimeMilliseconds(), sut.GetEntries().Single().TimestampEpochMillis);
    }

    [Fact]
    public void Add_RedactsIPv4_InMessageAndCategory()
    {
        var sut = new DiagnosticLogBuffer();

        sut.Add("10.0.0.1", "Connected to 192.168.1.10:5961");

        var entry = sut.GetEntries().Single();
        Assert.Equal("***.***.***.***/port", entry.Category);
        Assert.Equal("Connected to ***.***.***.***/port:5961", entry.Message);
    }

    [Fact]
    public void Add_RedactsIPv6()
    {
        var sut = new DiagnosticLogBuffer();

        sut.Add("Net", "fe80::1 reachable");

        Assert.Equal("[ipv6-redacted] reachable", sut.GetEntries().Single().Message);
    }

    [Fact]
    public void Add_LeavesPlainTextUntouched()
    {
        var sut = new DiagnosticLogBuffer();

        sut.Add("MYPC (Cam 1)", "status=Success sources=3");

        var entry = sut.GetEntries().Single();
        Assert.Equal("MYPC (Cam 1)", entry.Category);
        Assert.Equal("status=Success sources=3", entry.Message);
    }

    [Fact]
    public void Add_DefaultLevelIsInfo_AndExplicitLevelIsKept()
    {
        var sut = new DiagnosticLogBuffer();

        sut.Add("C", "m");
        sut.Add("C", "m", DiagnosticLogBuffer.LogLevel.Error);

        var entries = sut.GetEntries();
        Assert.Equal(DiagnosticLogBuffer.LogLevel.Info, entries[0].Level);
        Assert.Equal(DiagnosticLogBuffer.LogLevel.Error, entries[1].Level);
    }

    [Fact]
    public void Add_Beyond200Entries_DropsOldestFirst()
    {
        var sut = new DiagnosticLogBuffer();

        for (var i = 0; i <= 204; i++)
            sut.Add("C", $"m{i}");

        var entries = sut.GetEntries();
        Assert.Equal(200, entries.Count);
        Assert.Equal("m5", entries[0].Message);
        Assert.Equal("m204", entries[199].Message);
    }

    [Fact]
    public void Clear_EmptiesBuffer()
    {
        var sut = new DiagnosticLogBuffer();
        sut.Add("C", "m1");
        sut.Add("C", "m2");
        sut.Add("C", "m3");

        sut.Clear();

        Assert.Empty(sut.GetEntries());

        sut.Add("C", "after-clear");
        Assert.Equal("after-clear", sut.GetEntries().Single().Message);
    }
}
