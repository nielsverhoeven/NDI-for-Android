using NdiForAndroid.Features.DiagOverlay.Services;
using NdiForAndroid.Features.DiagOverlay.ViewModels;
using NdiForAndroid.Services;
using Xunit;

namespace NdiForAndroid.Tests.Features.DiagOverlay;

public class DiagnosticLogViewModelTests
{
    [Fact]
    public void Constructor_LoadsExistingEntries()
    {
        var service = new DiagnosticOverlayService();
        service.LogBuffer.Add("NDI-Bridge", "hello", DiagnosticLogBuffer.LogLevel.Warning);

        var sut = new DiagnosticLogViewModel(service, new FakeMainThreadDispatcher());

        Assert.Single(sut.LogEntries);
        var entry = sut.LogEntries[0];
        Assert.Equal("NDI-Bridge", entry.Category);
        Assert.Equal("hello", entry.Message);
        Assert.Equal(DiagnosticLogBuffer.LogLevel.Warning, entry.Level);
    }

    [Fact]
    public void ClearCommand_EmptiesBufferAndLogEntries()
    {
        var service = new DiagnosticOverlayService();
        service.LogBuffer.Add("C", "m1");
        service.LogBuffer.Add("C", "m2");
        var sut = new DiagnosticLogViewModel(service, new FakeMainThreadDispatcher());

        sut.ClearCommand.Execute(null);

        Assert.Empty(sut.LogEntries);
        Assert.Empty(service.LogBuffer.GetEntries());
    }

    [Fact]
    public void LogEntries_RaisesPropertyChanged_OnClear()
    {
        var service = new DiagnosticOverlayService();
        service.LogBuffer.Add("C", "m1");
        var sut = new DiagnosticLogViewModel(service, new FakeMainThreadDispatcher());

        var raised = false;
        sut.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(DiagnosticLogViewModel.LogEntries))
                raised = true;
        };

        sut.ClearCommand.Execute(null);

        Assert.True(raised);
    }
}
