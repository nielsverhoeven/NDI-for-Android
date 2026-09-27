using NdiForAndroid.Features.DiagOverlay.Services;
using NdiForAndroid.Features.DiagOverlay.ViewModels;
using NdiForAndroid.Services;
using Xunit;
using MsFakeTimeProvider = Microsoft.Extensions.Time.Testing.FakeTimeProvider;

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

    // --- #437: the page is reachable again, so its refresh timer must not outlive a visit ---

    [Fact]
    public void LogEntry_LevelLabel_WrapsTheLevelInBrackets()
    {
        var entry = new LogEntryViewModel(0, "Link", "Wi-Fi 5 GHz", DiagnosticLogBuffer.LogLevel.Warning);

        Assert.Equal("[Warning]", entry.LevelLabel);
    }

    [Fact]
    public void Route_IsTheShellRouteTheSettingsEntryPointNavigatesTo()
    {
        Assert.Equal("diagnostic-log", DiagnosticLogViewModel.Route);
    }

    [Fact]
    public void WithoutActivate_NoRefreshTimerRuns()
    {
        var service = new DiagnosticOverlayService();
        var time = new MsFakeTimeProvider();
        var sut = new DiagnosticLogViewModel(service, new FakeMainThreadDispatcher(), time);

        service.LogBuffer.Add("Link", "Wi-Fi 5 GHz, -55 dBm, 866 Mbit/s");
        time.Advance(TimeSpan.FromSeconds(10));

        Assert.Empty(sut.LogEntries);
    }

    [Fact]
    public void Activate_RefreshesTheListEveryTwoSeconds()
    {
        var service = new DiagnosticOverlayService();
        var time = new MsFakeTimeProvider();
        var sut = new DiagnosticLogViewModel(service, new FakeMainThreadDispatcher(), time);
        sut.Activate();

        service.LogBuffer.Add("Link", "Wi-Fi 5 GHz, -55 dBm, 866 Mbit/s");
        Assert.Empty(sut.LogEntries);

        time.Advance(TimeSpan.FromSeconds(2));

        Assert.Single(sut.LogEntries);
    }

    [Fact]
    public void Activate_RefreshesImmediately_SoAReturningVisitSeesNewEntries()
    {
        var service = new DiagnosticOverlayService();
        var sut = new DiagnosticLogViewModel(service, new FakeMainThreadDispatcher(), new MsFakeTimeProvider());

        service.LogBuffer.Add("Link", "Wi-Fi 2.4 GHz, -78 dBm, 6 Mbit/s");
        sut.Activate();

        Assert.Single(sut.LogEntries);
    }

    [Fact]
    public void Deactivate_StopsRefreshing()
    {
        var service = new DiagnosticOverlayService();
        var time = new MsFakeTimeProvider();
        var sut = new DiagnosticLogViewModel(service, new FakeMainThreadDispatcher(), time);
        sut.Activate();
        sut.Deactivate();

        service.LogBuffer.Add("Link", "Wi-Fi 5 GHz, -55 dBm, 866 Mbit/s");
        time.Advance(TimeSpan.FromSeconds(10));

        Assert.Empty(sut.LogEntries);
    }
}
