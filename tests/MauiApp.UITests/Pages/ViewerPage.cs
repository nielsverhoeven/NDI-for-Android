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
    /// True while a stream is playing, in either the windowed Deck/Sheet or the full-screen
    /// overlay — both carry <c>viewer.stop</c> (#384 slice 3), and only one of them is ever on
    /// screen at a time (mirrors the existing pane/page duplicate-id pattern).
    /// </summary>
    public bool IsPlaying => IsPresent(TestIds.ViewerStop);

    public bool IsReconnecting  => IsPresent(TestIds.ViewerCancelRetry);
    public bool CanReconnect    => IsPresent(TestIds.ViewerReconnect);
    public bool IsPtzSupported  => IsPresent(TestIds.ViewerPtzAutoFocus);

    /// <summary>
    /// Waits for playback to start in the windowed Deck/Sheet layout — a network-budget wait for
    /// `viewer.stop` (bound to `IsPlaying`), followed by a bounded wait for the full-screen
    /// overlay to be gone. The second wait matters because `viewer.stop` now also exists inside
    /// `FullScreenControlsOverlay` (#384 slice 3): calling this right after `ExitFullScreen()` or
    /// an orientation-driven exit must observe the Deck/Sheet actually re-rendered, not just any
    /// `viewer.stop` node — and on a compact device an exit can involve a real OS rotation, hence
    /// `Timeouts.Navigation` rather than the shorter `Timeouts.StateChange`.
    /// </summary>
    public void WaitUntilPlaying()
    {
        WaitFor(TestIds.ViewerStop, Timeouts.Network, "The viewer never reported playback");

        var deadline = DateTime.UtcNow + Timeouts.Navigation;
        while (IsFullScreen && DateTime.UtcNow < deadline)
            Thread.Sleep(100);

        if (IsFullScreen)
            throw new InvalidOperationException(
                $"The viewer reported playback but was still full screen after " +
                $"{Timeouts.Navigation.TotalSeconds:0}s — the windowed Deck/Sheet never re-rendered.");
    }

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
    /// True while full screen is showing. Reads the overlay's own stable root id
    /// (#384 slice 3) rather than inferring from `IsPlaying`: the overlay now also carries
    /// `viewer.stop`/`viewer.audioToggle`/`viewer.quality.*`/`viewer.ptz.*` (reused from the
    /// Deck/Sheet), so `!IsPlaying` no longer distinguishes "full screen" from "not playing".
    /// Unlike the old `viewer.fullScreen.qualityCycle`-based `WaitUntilFullScreen` id, this one
    /// does not depend on the 2.5s/5s auto-hide, so it stays valid for the lifetime of the state.
    /// </summary>
    public bool IsFullScreen => IsPresent(TestIds.ViewerFullScreenOverlay);

    /// <summary>
    /// Blocks until full screen has actually rendered. On a compact device the button-triggered
    /// path now involves a real OS orientation change before the overlay appears, hence
    /// `Timeouts.Navigation` rather than the shorter `Timeouts.StateChange` used before #384 slice 3.
    /// </summary>
    public void WaitUntilFullScreen() =>
        WaitFor(TestIds.ViewerFullScreenOverlay, Timeouts.Navigation, "Full screen did not engage");

    /// <summary>Single-taps the video surface — re-shows the full-screen overlay if the 3s
    /// auto-hide has already fired, or toggles the overlay in the windowed layout.</summary>
    public void TapVideo() => Tap(TestIds.ViewerVideoBorder);

    /// <summary>
    /// Exits full screen. The single tap on the video *toggles* the overlay since #384 slice 3, so
    /// tapping unconditionally would hide the very button this method then needs; tap only when the
    /// 2.5s/5s auto-hide has already taken the toolbar away, and re-check rather than assume.
    /// </summary>
    public void ExitFullScreen()
    {
        if (!IsPresent(TestIds.ViewerFullScreenToggle))
            TapVideo();

        if (!IsPresent(TestIds.ViewerFullScreenToggle))
            TapVideo();   // the auto-hide can fire between the check and the tap; one retry is enough

        ToggleFullScreen();
    }

    /// <summary>Shows/hides the full-screen camera (PTZ) controls layer.</summary>
    public void ToggleCameraLayer() => Tap(TestIds.ViewerFullScreenCamera);

    public void PanUp()    => Tap(TestIds.ViewerPtzUp);
    public void PanDown()  => Tap(TestIds.ViewerPtzDown);
    public void PanLeft()  => Tap(TestIds.ViewerPtzLeft);
    public void PanRight() => Tap(TestIds.ViewerPtzRight);
    public void AutoFocus()=> Tap(TestIds.ViewerPtzAutoFocus);
    public void ZoomIn()   => Tap(TestIds.ViewerPtzZoomIn);
    public void ZoomOut()  => Tap(TestIds.ViewerPtzZoomOut);
}
