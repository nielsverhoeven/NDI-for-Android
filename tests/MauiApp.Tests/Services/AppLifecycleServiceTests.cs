using NdiForAndroid.Services;
using Xunit;

namespace NdiForAndroid.Tests.Services;

public class AppLifecycleServiceTests
{
    [Fact]
    public void NotifyResumed_RaisesAppResumedEvent()
    {
        var sut = new AppLifecycleService();
        var raised = false;
        sut.AppResumed += () => raised = true;

        sut.NotifyResumed();

        Assert.True(raised);
    }

    [Fact]
    public void NotifyPaused_RaisesAppPausedEvent()
    {
        var sut = new AppLifecycleService();
        var raised = false;
        sut.AppPaused += () => raised = true;

        sut.NotifyPaused();

        Assert.True(raised);
    }

    [Fact]
    public void NotifyResumed_UpdatesStateBeforeRaisingEvent()
    {
        var sut = new AppLifecycleService();
        bool isInForegroundDuringEvent = false;
        sut.AppResumed += () => isInForegroundDuringEvent = sut.IsInForeground;

        sut.NotifyResumed();

        Assert.True(isInForegroundDuringEvent);
    }

    [Fact]
    public void NotifyPaused_UpdatesStateBeforeRaisingEvent()
    {
        var sut = new AppLifecycleService();
        sut.NotifyResumed();
        bool isInForegroundDuringEvent = true;
        sut.AppPaused += () => isInForegroundDuringEvent = sut.IsInForeground;

        sut.NotifyPaused();

        Assert.False(isInForegroundDuringEvent);
    }
}
