using OpenQA.Selenium;
using NdiForAndroid.UITests.Infrastructure;
using NdiForAndroid.UITests.Pages;
using Xunit;

namespace NdiForAndroid.UITests;

/// <summary>#346 OUT-07: the whole switch row — not only the thumb — toggles the setting.</summary>
[Collection("AppiumSession")]
public sealed class OutputPageTests : UiTestBase
{
    public OutputPageTests(AppiumDriverFixture fixture) : base(fixture) { }

    [RetryableSkippableFact]
    public void Output_TappingTheReStreamLabel_TogglesTheModeSwitch() => Run(app =>
    {
        app.Rotate(ScreenOrientation.Portrait);
        app.Navigation.GoTo(NavDestination.Stream);
        app.Output.WaitUntilVisible();

        var before = app.Output.IsReStreamMode;
        try
        {
            app.Output.TapReStreamLabel();
            WaitUntil(() => app.Output.IsReStreamMode != before);
            Assert.NotEqual(before, app.Output.IsReStreamMode);
        }
        finally
        {
            // Restore capture mode so later tests see the default.
            if (app.Output.IsReStreamMode != before)
                app.Output.TapReStreamLabel();
        }
    });

    [RetryableSkippableFact]
    public void Output_TappingTheMicrophoneLabel_TogglesTheMicrophoneSwitch() => Run(app =>
    {
        app.Rotate(ScreenOrientation.Portrait);
        app.Navigation.GoTo(NavDestination.Stream);
        app.Output.WaitUntilVisible();

        var before = app.Output.IsMicrophoneOn;
        try
        {
            app.Output.TapMicrophoneLabel();
            WaitUntil(() => app.Output.IsMicrophoneOn != before);
            Assert.NotEqual(before, app.Output.IsMicrophoneOn);
        }
        finally
        {
            if (app.Output.IsMicrophoneOn != before)
                app.Output.TapMicrophoneLabel();
        }
    });

    [RetryableSkippableFact]
    public void Output_TappingTheMicrophoneSwitchItself_TogglesExactlyOnce() => Run(app =>
    {
        app.Rotate(ScreenOrientation.Portrait);
        app.Navigation.GoTo(NavDestination.Stream);
        app.Output.WaitUntilVisible();

        var before = app.Output.IsMicrophoneOn;
        try
        {
            app.Output.ToggleMicrophone();
            WaitUntil(() => app.Output.IsMicrophoneOn != before);
            Assert.NotEqual(before, app.Output.IsMicrophoneOn);
        }
        finally
        {
            if (app.Output.IsMicrophoneOn != before)
                app.Output.ToggleMicrophone();
        }
    });

    private static void WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + Timeouts.StateChange;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return;

            Thread.Sleep(100);
        }
    }
}
