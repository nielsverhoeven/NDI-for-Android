using OpenQA.Selenium.Appium.Android;
using NdiForAndroid.Features.Viewer.Models;
using NdiForAndroid.NdiBridge;
using NdiForAndroid.Testing;
using NdiForAndroid.UITests.Infrastructure;

namespace NdiForAndroid.UITests.Pages;

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
    /// True while the source echoes program tally and the "ON PROGRAM" badge is showing (#350).
    /// Never true on the CI emulator (x86_64 has no NDI runtime, so nothing ever plays) — this is
    /// for device runs against a real switcher/sender; do not write an emulator test around it.
    /// </summary>
    public bool IsOnProgram => IsPresent(TestIds.ViewerTallyProgramBadge);

    /// <summary>True while the "Stopped" overlay is showing over the frozen last frame (#348).</summary>
    public bool IsStoppedOverlayVisible => IsPresent(TestIds.ViewerStoppedBadge);

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

    public void SelectQuality(QualityProfile profile) => Tap(QualityProfileOption.AutomationIdFor(profile));

    /// <summary>"Quality: Balanced" while playing; the label is not rendered while idle.</summary>
    public string QualityLabel => TextOf(TestIds.ViewerQualityLabel);

    /// <summary>True while the advisory "Connection weak…" hint is shown (#331); never on the emulator.</summary>
    public bool HasConnectionHint => IsPresent(TestIds.ViewerConnectionHint);
    public string ConnectionHint => TextOf(TestIds.ViewerConnectionHint);

    /// <summary>Full-screen toolbar S/B/H button: Smooth → Balanced → High → Smooth.</summary>
    public void CycleQuality() => Tap(TestIds.ViewerQualityCycle);

    public void ToggleAudio()  => Tap(TestIds.ViewerAudioToggle);
    public void Stop()         => Tap(TestIds.ViewerStop);
    public void CancelRetry()  => Tap(TestIds.ViewerCancelRetry);
    public void Reconnect()    => Tap(TestIds.ViewerReconnect);

    /// <summary>Toggles the full-screen overlay. Present in both the windowed sheet and the
    /// full-screen toolbar, which never show at the same time (#343/#345).</summary>
    public void ToggleFullScreen() => Tap(TestIds.ViewerFullScreenToggle);

    /// <summary>What TalkBack would announce for the full-screen toggle — state-dependent
    /// ("Enter full screen" / "Exit full screen"), not the same name in both states.</summary>
    public string FullScreenToggleLabel =>
        WaitFor(TestIds.ViewerFullScreenToggle).GetAttribute("content-desc") ?? string.Empty;

    /// <summary>
    /// True while full screen is showing. The full-screen overlay's own Stop button has no
    /// AutomationId (only the Deck/Sheet one does), so "the video canvas is present but no
    /// id'd Stop button is" is exactly full screen — it cannot be confused with "not playing",
    /// because a source must already be playing to reach either state in this suite.
    /// </summary>
    public bool IsFullScreen => HasVideoSurface && !IsPlaying;

    /// <summary>
    /// Blocks until full screen has actually rendered. `viewer.fullScreen.qualityCycle` exists only
    /// in FullScreenControlsOverlay, so its appearance is proof the overlay is up — unlike the
    /// non-waiting IsFullScreen read, which races MAUI's UI-thread property-change cascade. Call
    /// this only immediately after entering full screen: the id disappears again when the 3s
    /// auto-hide fires.
    /// </summary>
    public void WaitUntilFullScreen() =>
        WaitFor(TestIds.ViewerQualityCycle, Timeouts.StateChange, "Full screen did not engage");

    /// <summary>Single-taps the video surface — re-shows the full-screen overlay if the 3s
    /// auto-hide has already fired, or toggles the overlay in the windowed layout.</summary>
    public void TapVideo() => Tap(TestIds.ViewerVideoBorder);

    /// <summary>
    /// Exits full screen. Taps the video first to guarantee the overlay (and its toggle/exit
    /// button) is on screen even if the 3s auto-hide already fired since entering — tapping the
    /// button id directly would time out waiting for an element that auto-hid moments earlier.
    /// </summary>
    public void ExitFullScreen()
    {
        TapVideo();
        ToggleFullScreen();
    }

    public void PanUp()    => Tap(TestIds.ViewerPtzUp);
    public void PanDown()  => Tap(TestIds.ViewerPtzDown);
    public void PanLeft()  => Tap(TestIds.ViewerPtzLeft);
    public void PanRight() => Tap(TestIds.ViewerPtzRight);
    public void AutoFocus()=> Tap(TestIds.ViewerPtzAutoFocus);
    public void ZoomIn()   => Tap(TestIds.ViewerPtzZoomIn);
    public void ZoomOut()  => Tap(TestIds.ViewerPtzZoomOut);
}
