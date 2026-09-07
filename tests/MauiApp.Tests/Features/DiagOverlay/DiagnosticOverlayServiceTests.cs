using NdiForAndroid.Features.DiagOverlay.Services;
using Xunit;

namespace NdiForAndroid.Tests.Features.DiagOverlay;

public class DiagnosticOverlayServiceTests
{
    private sealed class RecordingSink : IDiagnosticLogSink
    {
        public List<(string Tag, string Message)> Lines { get; } = new();
        public bool Throw { get; set; }

        public void Debug(string tag, string message)
        {
            if (Throw) throw new InvalidOperationException("sink failure");
            Lines.Add((tag, message));
        }
    }

    [Fact]
    public void Defaults_ViewerSnapshotIsZeroed_AndDiscoverySnapshotSaysNoRunYet()
    {
        var sut = new DiagnosticOverlayService();

        var viewer = sut.GetCurrentViewerDiagnostics();
        Assert.Equal(new ViewerDiagnosticSnapshot(0f, 0f, 0, 0, string.Empty), viewer);

        var discovery = sut.GetCurrentDiscoveryDiagnostics();
        Assert.Equal(new DiscoveryDiagnosticSnapshot("No discovery run yet", 0, null), discovery);

        Assert.Empty(sut.LogBuffer.GetEntries());
    }

    [Fact]
    public void UpdateViewerDiagnostics_ReplacesSnapshot()
    {
        var sut = new DiagnosticOverlayService();

        sut.UpdateViewerDiagnostics(29.97f, 1.5f, 1920, 1080, "PC (Cam)");

        Assert.Equal(new ViewerDiagnosticSnapshot(29.97f, 1.5f, 1920, 1080, "PC (Cam)"), sut.GetCurrentViewerDiagnostics());
    }

    [Fact]
    public void UpdateDiscoveryDiagnostics_ReplacesSnapshot_WithAndWithoutDuration()
    {
        var sut = new DiagnosticOverlayService();

        sut.UpdateDiscoveryDiagnostics("Success", 3, TimeSpan.FromSeconds(1.5));
        Assert.Equal(new DiscoveryDiagnosticSnapshot("Success", 3, TimeSpan.FromSeconds(1.5)), sut.GetCurrentDiscoveryDiagnostics());

        sut.UpdateDiscoveryDiagnostics("Failure", 0);
        Assert.Equal(new DiscoveryDiagnosticSnapshot("Failure", 0, null), sut.GetCurrentDiscoveryDiagnostics());
    }

    [Fact]
    public void IsDeveloperMode_Toggle_AppendsOneBufferEntryPerChange()
    {
        var sut = new DiagnosticOverlayService();

        sut.IsDeveloperMode = true;
        sut.IsDeveloperMode = true; // no change - no new entry
        sut.IsDeveloperMode = false;

        var entries = sut.LogBuffer.GetEntries();
        Assert.Equal(2, entries.Count);
        Assert.All(entries, e => Assert.Equal("DevOverlay", e.Category));
        Assert.Equal("Developer mode enabled", entries[0].Message);
        Assert.Equal("Developer mode disabled", entries[1].Message);
        Assert.All(entries, e => Assert.Equal(DiagnosticLogBuffer.LogLevel.Info, e.Level));

        Assert.False(sut.IsDeveloperMode);
    }

    [Fact]
    public void UpdateDiscoveryDiagnostics_DeveloperModeOff_DoesNotTouchSink()
    {
        var sink = new RecordingSink();
        var sut = new DiagnosticOverlayService(sink);

        sut.UpdateDiscoveryDiagnostics("Success", 3, TimeSpan.FromSeconds(1));

        Assert.Empty(sink.Lines);
        Assert.Equal("Success", sut.GetCurrentDiscoveryDiagnostics().LastStatus);
    }

    [Fact]
    public void UpdateDiscoveryDiagnostics_DeveloperModeOn_MirrorsExactlyOneLine()
    {
        var sink = new RecordingSink();
        var sut = new DiagnosticOverlayService(sink) { IsDeveloperMode = true };

        sut.UpdateDiscoveryDiagnostics("Success", 3, TimeSpan.FromMilliseconds(1500));

        Assert.Equal(new List<(string, string)> { (DiagnosticOverlayService.DiscoveryLogTag, "status=\"Success\" sources=3 durationMs=1500") }, sink.Lines);
    }

    [Fact]
    public void UpdateDiscoveryDiagnostics_NullDuration_MirrorsMinusOne()
    {
        var sink = new RecordingSink();
        var sut = new DiagnosticOverlayService(sink) { IsDeveloperMode = true };

        sut.UpdateDiscoveryDiagnostics("Failure", 0);

        Assert.Equal("status=\"Failure\" sources=0 durationMs=-1", sink.Lines.Single().Message);
    }

    [Fact]
    public void UpdateDiscoveryDiagnostics_SinkThrows_IsSwallowed_AndSnapshotStillUpdated()
    {
        var sink = new RecordingSink { Throw = true };
        var sut = new DiagnosticOverlayService(sink) { IsDeveloperMode = true };

        sut.UpdateDiscoveryDiagnostics("Success", 1);

        Assert.Equal("Success", sut.GetCurrentDiscoveryDiagnostics().LastStatus);
    }

    [Fact]
    public void UpdateDiscoveryDiagnostics_NoSink_DeveloperModeOn_DoesNotThrow()
    {
        var sut = new DiagnosticOverlayService { IsDeveloperMode = true };

        sut.UpdateDiscoveryDiagnostics("Success", 1);
    }
}
