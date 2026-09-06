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

    [Fact]
    public void NotifyConfigurationChanged_LandscapeTrueFromDefault_RaisesOrientationChangedWithTrue()
    {
        var sut = new AppLifecycleService();
        bool? raisedValue = null;
        sut.OrientationChanged += value => raisedValue = value;

        sut.NotifyConfigurationChanged(isLandscape: true, smallestWidthDp: 600d);

        Assert.True(raisedValue);
    }

    [Fact]
    public void NotifyConfigurationChanged_LandscapeFalseAfterTrue_RaisesOrientationChangedWithFalse()
    {
        var sut = new AppLifecycleService();
        sut.NotifyConfigurationChanged(isLandscape: true, smallestWidthDp: 600d);
        bool? raisedValue = null;
        sut.OrientationChanged += value => raisedValue = value;

        sut.NotifyConfigurationChanged(isLandscape: false, smallestWidthDp: 320d);

        Assert.False(raisedValue);
    }

    [Fact]
    public void NotifyConfigurationChanged_CalledAgainWithSameOrientation_DoesNotRaiseOrientationChanged()
    {
        var sut = new AppLifecycleService();
        sut.NotifyConfigurationChanged(isLandscape: true, smallestWidthDp: 600d);
        var raised = false;
        sut.OrientationChanged += _ => raised = true;

        sut.NotifyConfigurationChanged(isLandscape: true, smallestWidthDp: 600d);

        Assert.False(raised);
    }

    [Fact]
    public void NotifyConfigurationChanged_UpdatesIsLandscapeBeforeRaisingOrientationChanged()
    {
        var sut = new AppLifecycleService();
        bool isLandscapeDuringEvent = false;
        sut.OrientationChanged += _ => isLandscapeDuringEvent = sut.IsLandscape;

        sut.NotifyConfigurationChanged(isLandscape: true, smallestWidthDp: 600d);

        Assert.True(isLandscapeDuringEvent);
    }

    [Fact]
    public void NotifyConfigurationChanged_OrientationChanges_SmallestWidthDpHoldsNewValueDuringEvent()
    {
        var sut = new AppLifecycleService();
        double smallestWidthDpDuringEvent = -1;
        sut.OrientationChanged += _ => smallestWidthDpDuringEvent = sut.SmallestWidthDp;
        sut.NotifyConfigurationChanged(isLandscape: true, smallestWidthDp: 600);
        Assert.Equal(600d, smallestWidthDpDuringEvent);
    }

    [Fact]
    public void NotifyConfigurationChanged_SmallestWidthDpUpdated_EvenWhenOrientationUnchanged()
    {
        var sut = new AppLifecycleService();
        sut.NotifyConfigurationChanged(isLandscape: true, smallestWidthDp: 600d);

        sut.NotifyConfigurationChanged(isLandscape: true, smallestWidthDp: 720d);

        Assert.Equal(720d, sut.SmallestWidthDp);
    }

    [Fact]
    public void NotifyConfigurationChanged_UpdatesSmallestWidthDpProperty()
    {
        var sut = new AppLifecycleService();

        sut.NotifyConfigurationChanged(isLandscape: false, smallestWidthDp: 480d);

        Assert.Equal(480d, sut.SmallestWidthDp);
    }
}
