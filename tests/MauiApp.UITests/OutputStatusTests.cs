using NdiForAndroid.UITests.Infrastructure;
using NdiForAndroid.UITests.Pages;
using Xunit;

namespace NdiForAndroid.UITests;

/// <summary>#349: a failed Start must look different from an informational status.</summary>
[Collection("AppiumSession")]
public sealed class OutputStatusTests : UiTestBase
{
    public OutputStatusTests(AppiumDriverFixture fixture) : base(fixture) { }

    [RetryableSkippableFact]
    public void OutputStatus_FailedStart_IsMarkedAsError() => Run(app =>
    {
        app.ResetToHome();
        app.Navigation.GoTo(NavDestination.Stream);
        app.Output.WaitUntilVisible();

        // Negative control: the informational 'Tap Start…' hint must not carry the error cue,
        // otherwise the positive assertion below would pass on a glyph that is always shown.
        Assert.False(app.Output.IsStatusError,
            $"The informational status '{app.Output.Status}' is showing the error glyph");

        app.Output.Start();

        // The CI emulator (x86_64) has no libndi.so: NdiOutputBridge.StartOutputCoreAsync throws
        // at EnsureInitialized before any MediaProjection consent, so Start fails immediately and
        // without a system dialog.
        var deadline = DateTime.UtcNow + Timeouts.Network;
        while (DateTime.UtcNow < deadline && !app.Output.IsStatusError && !app.Output.IsOutputActive)
            Thread.Sleep(250);

        var status = app.Output.Status;
        var failed = status.Contains("failed", StringComparison.OrdinalIgnoreCase);

        // A device with a working NDI runtime never reaches the failure branch (it shows the
        // consent dialog or starts streaming) — that is device-only coverage, not a failure here.
        Skip.If(app.Output.IsOutputActive || !failed,
            $"Start did not fail on this device (status: '{status}'); the error styling can only be exercised where NDI is unavailable.");

        Assert.True(app.Output.IsStatusError,
            $"Start failed ('{status}') but the status line is not marked as an error — no warning glyph, so it is indistinguishable from an informational message.");

        // Colour: red-dominant text, not the neutral TextPrimary. This is not a hex pin — only a
        // sanity check that the rendered colour leans red rather than the neutral text colour.
        using var screen = app.CaptureScreen();
        var label = app.Output.StatusElement;
        var background = screen.DominantColorOf(label, inset: 0.1);
        var text = screen.MostContrastingColorIn(label, background, inset: 0.1);
        Assert.True(text.R > text.G + 20 && text.R > text.B + 20,
            $"Error status text sampled as {text} — expected red-leaning text, not the neutral text colour.");
    });
}
