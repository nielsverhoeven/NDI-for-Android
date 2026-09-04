using OpenQA.Selenium.Appium.Android;
using NdiForAndroid.Testing;
using NdiForAndroid.UITests.Infrastructure;

namespace NdiForAndroid.UITests.Pages;

/// <summary>Receive quality profiles offered by the viewer.</summary>
public enum QualityProfile
{
    Smooth,
    Balanced,
    High,
}

/// <summary>
/// The viewer: video surface plus quality, audio, PTZ and reconnect controls.
/// </summary>
/// <remarks>
/// The same <c>ViewerView</c> is used twice — as the whole of <c>ViewerPage</c> and as the
/// Expanded-window pane inside the source list — so its control ids can appear twice in one tree.
/// This object targets the full page; the pane is reached through
/// <see cref="SourceListPage.IsViewerPaneVisible"/>.
/// </remarks>
public sealed class ViewerPage : PageObject
{
    public ViewerPage(AndroidDriver driver) : base(driver) { }

    protected override string PageId => TestIds.ViewerPage;
    public override string Name => "Viewer";

    public string Status => TextOf(TestIds.ViewerStatus);

    /// <summary>True when the SkiaSharp video surface is on screen.</summary>
    public bool HasVideoSurface => IsPresent(TestIds.ViewerVideoCanvas);

    /// <summary>
    /// True while a stream is playing — inferred from the Stop button, which is bound to
    /// <c>IsPlaying</c>.
    /// </summary>
    public bool IsPlaying => IsPresent(TestIds.ViewerStop);

    public bool IsReconnecting  => IsPresent(TestIds.ViewerCancelRetry);
    public bool CanReconnect    => IsPresent(TestIds.ViewerReconnect);
    public bool IsPtzSupported  => IsPresent(TestIds.ViewerPtzAutoFocus);

    /// <summary>Waits for playback to start — a network-budget wait, not an element one.</summary>
    public void WaitUntilPlaying() =>
        WaitFor(TestIds.ViewerStop, Timeouts.Network, "The viewer never reported playback");

    public void SelectQuality(QualityProfile profile) => Tap(profile switch
    {
        QualityProfile.Smooth   => TestIds.ViewerQualitySmooth,
        QualityProfile.Balanced => TestIds.ViewerQualityBalanced,
        QualityProfile.High     => TestIds.ViewerQualityHigh,
        _ => throw new ArgumentOutOfRangeException(nameof(profile)),
    });

    public void ToggleAudio()  => Tap(TestIds.ViewerAudioToggle);
    public void Stop()         => Tap(TestIds.ViewerStop);
    public void CancelRetry()  => Tap(TestIds.ViewerCancelRetry);
    public void Reconnect()    => Tap(TestIds.ViewerReconnect);

    public void PanUp()    => Tap(TestIds.ViewerPtzUp);
    public void PanDown()  => Tap(TestIds.ViewerPtzDown);
    public void PanLeft()  => Tap(TestIds.ViewerPtzLeft);
    public void PanRight() => Tap(TestIds.ViewerPtzRight);
    public void AutoFocus()=> Tap(TestIds.ViewerPtzAutoFocus);
    public void ZoomIn()   => Tap(TestIds.ViewerPtzZoomIn);
    public void ZoomOut()  => Tap(TestIds.ViewerPtzZoomOut);
}
