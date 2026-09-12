using System.ComponentModel;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiForAndroid.Features.Viewer;
using NdiForAndroid.Services;

namespace NdiForAndroid.Features.Viewer.ViewModels;

public partial class ViewerViewModel
{
    private const double OverlayAutoHideSeconds = 2.5;
    private const double PtzLayerAutoHideSeconds = 5;
    private const int PendingOrientationTimeoutSeconds = 3;

    /// <summary>Orientation the app has asked the platform for and is waiting to observe, so the
    /// matching <see cref="IAppLifecycleService.OrientationChanged"/> can complete the full-screen
    /// transition and release the lock. <see cref="PendingOrientation.None"/> when nothing is
    /// pending.</summary>
    private enum PendingOrientation { None, Landscape, Portrait }

    private ITimer? _overlayAutoHideTimer;
    private ITimer? _pendingOrientationTimeoutTimer;
    private PendingOrientation _pendingOrientation = PendingOrientation.None;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AreControlsVisible))]
    private bool _isFullScreen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AreControlsVisible))]
    private bool _isControlsOverlayVisible = true;

    /// <summary>Whether the camera (PTZ) controls layer is showing inside the full-screen overlay
    /// (#384 slice 3). Default hidden: full screen opens on the clean video-only view, and the
    /// camera button reveals pad/zoom/presets on demand.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFullScreenPtzVisible))]
    private bool _isPtzLayerVisible;

    /// <summary>Controls are visible in normal mode, or in full screen while the overlay hasn't auto-hidden.</summary>
    public bool AreControlsVisible => !IsFullScreen || IsControlsOverlayVisible;

    /// <summary>Gates the full-screen overlay's preset grid, d-pad and zoom borders: PTZ must be
    /// available AND the user has opened the camera layer. The root overlay's own
    /// <see cref="AreControlsVisible"/> binding still gates all of it for auto-hide.</summary>
    public bool IsFullScreenPtzVisible => IsPtzControlActive && IsPtzLayerVisible;

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        if (e.PropertyName == nameof(IsPlaying))
        {
            _immersiveMode.KeepScreenOn(IsPlaying);
            EnterFullScreenIfPlayingInLandscapeOnCompactDevice();
        }
    }

    /// <summary>Keeps the compact-device invariant "IsFullScreen ⟺ landscape while playing"
    /// (#383/#384 design decision (b)) true on the one path no orientation event covers: playback
    /// that *starts* while the device is already in landscape — Watch tapped from the landscape
    /// rail, a reconnect completing, or a restore on resume. <see cref="IAppLifecycleService.OrientationChanged"/>
    /// only fires on an actual change, so without this the viewer renders the windowed Sheet
    /// layout in landscape, which is the #383 report itself. Never requests a rotation (the device
    /// is already landscape) and never fires while a rotation request is in flight.</summary>
    private void EnterFullScreenIfPlayingInLandscapeOnCompactDevice()
    {
        if (IsPlaying
            && !IsFullScreen
            && _pendingOrientation == PendingOrientation.None
            && _lifecycle.IsLandscape
            && ViewerControlLayout.IsCompactDevice(_lifecycle.SmallestWidthDp))
        {
            IsFullScreen = true;
        }
    }

    partial void OnIsFullScreenChanged(bool value)
    {
        if (value)
        {
            IsControlsOverlayVisible = true;
            ResetOverlayAutoHideTimer();
        }
        else
        {
            _overlayAutoHideTimer?.Dispose();
            _overlayAutoHideTimer = null;
            IsControlsOverlayVisible = true;
            IsPtzLayerVisible = false;
            // Single choke point for "full screen ended" — every exit path (button, Back, Stop(),
            // the portrait rotation completing, the 3s fallback, ForceExitFullScreen) converges
            // here, so the device can never be left pinned. Release() is idempotent.
            _orientationLock.Release();
        }
    }

    [RelayCommand]
    private void ToggleFullScreen()
    {
        if (!IsPlaying && !IsFullScreen)
            return;

        if (IsFullScreen)
        {
            BeginExitFullScreen();
            return;
        }

        BeginEnterFullScreen();
    }

    /// <summary>Toggles the full-screen controls overlay on a single tap of the video surface.
    /// No-op outside full screen — the windowed layout has its own always-on Deck/Sheet. Visible
    /// ⇒ hides immediately; hidden ⇒ shows and re-arms the auto-hide timer (#384 slice 3 — replaces
    /// the removed double-tap-to-full-screen gesture and the old show-only command).</summary>
    [RelayCommand]
    private void ToggleControlsOverlay()
    {
        if (!IsFullScreen)
            return;

        if (IsControlsOverlayVisible)
        {
            _overlayAutoHideTimer?.Dispose();
            _overlayAutoHideTimer = null;
            IsControlsOverlayVisible = false;
        }
        else
        {
            ResetOverlayAutoHideTimer();
        }
    }

    /// <summary>Shows/hides the camera (PTZ) controls layer inside full screen. Auto-hide runs
    /// longer (5s) while the layer is open — camera aiming has pauses between nudges longer than
    /// the 2.5s minimal-overlay budget.</summary>
    [RelayCommand]
    private void TogglePtzLayer()
    {
        IsPtzLayerVisible = !IsPtzLayerVisible;
        NotifyControlInteraction();
    }

    /// <summary>Resets the auto-hide countdown while full screen; no-op otherwise.</summary>
    public void NotifyControlInteraction()
    {
        if (IsFullScreen)
            ResetOverlayAutoHideTimer();
    }

    private void ResetOverlayAutoHideTimer()
    {
        IsControlsOverlayVisible = true;
        var due = TimeSpan.FromSeconds(IsPtzLayerVisible ? PtzLayerAutoHideSeconds : OverlayAutoHideSeconds);

        if (_overlayAutoHideTimer is null)
            _overlayAutoHideTimer = _timeProvider.CreateTimer(
                _ => _dispatcher.BeginInvokeOnMainThread(HideControlsOverlay), null, due, Timeout.InfiniteTimeSpan);
        else
            _overlayAutoHideTimer.Change(due, Timeout.InfiniteTimeSpan);
    }

    private void HideControlsOverlay()
    {
        if (IsFullScreen)
            IsControlsOverlayVisible = false;
    }

    // ----- Orientation-driven full screen (#383/#384 slice 3) --------------

    /// <summary>Enters full screen from the toggle button: on a compact device still in portrait,
    /// requests landscape and waits for the resulting <see cref="IAppLifecycleService.OrientationChanged"/>
    /// to actually flip <see cref="IsFullScreen"/> (one code path with the rotate-triggered
    /// auto-enter in <see cref="HandleOrientationChanged"/>); everywhere else — tablet, or a
    /// compact device already in landscape — enters directly.</summary>
    private void BeginEnterFullScreen()
    {
        if (ViewerControlLayout.IsCompactDevice(_lifecycle.SmallestWidthDp) && !_lifecycle.IsLandscape)
        {
            RequestPendingOrientation(PendingOrientation.Landscape);
            return;
        }

        IsFullScreen = true;
    }

    /// <summary>Exits full screen from the toggle button, Back, or <see cref="Stop"/>: on a compact
    /// device still in landscape, requests portrait and waits for the matching orientation change;
    /// everywhere else exits directly. Also unconditionally abandons a still-pending landscape
    /// *entry* request (e.g. Stop() racing a not-yet-completed rotation) so it can never resurrect
    /// full screen later for a stream that has already stopped. A pending *exit* already in flight
    /// is a no-op, not a second platform call — this is what lets a second Back press during the
    /// pending window be safely swallowed by the caller.</summary>
    private void BeginExitFullScreen()
    {
        if (_pendingOrientation == PendingOrientation.Landscape)
        {
            ClearPendingOrientation();
            _orientationLock.Release();
        }

        if (!IsFullScreen)
            return;

        if (_pendingOrientation == PendingOrientation.Portrait)
            return;

        if (ViewerControlLayout.IsCompactDevice(_lifecycle.SmallestWidthDp) && _lifecycle.IsLandscape)
        {
            RequestPendingOrientation(PendingOrientation.Portrait);
            return;
        }

        IsFullScreen = false;
    }

    private void RequestPendingOrientation(PendingOrientation target)
    {
        _pendingOrientation = target;

        if (target == PendingOrientation.Landscape)
            _orientationLock.RequestLandscape();
        else
            _orientationLock.RequestPortrait();

        _pendingOrientationTimeoutTimer?.Dispose();
        _pendingOrientationTimeoutTimer = _timeProvider.CreateTimer(
            _ => _dispatcher.BeginInvokeOnMainThread(OnPendingOrientationTimeout),
            null, TimeSpan.FromSeconds(PendingOrientationTimeoutSeconds), Timeout.InfiniteTimeSpan);
    }

    /// <summary>3s fallback for a requested rotation that never arrives (e.g. a platform that
    /// ignores <c>RequestedOrientation</c>). Symmetric in both directions: the enter side still
    /// enters full screen (in portrait — the same behaviour YouTube itself uses), the exit side
    /// still exits. The enter side releases the lock explicitly (entering never runs through
    /// <see cref="OnIsFullScreenChanged"/>'s release choke point); the exit side lets that choke
    /// point release it instead, so the lock is not released twice.</summary>
    private void OnPendingOrientationTimeout()
    {
        var pending = _pendingOrientation;
        if (pending == PendingOrientation.None)
            return;

        ClearPendingOrientation();

        if (pending == PendingOrientation.Landscape)
        {
            _orientationLock.Release();
            IsFullScreen = true;
        }
        else
        {
            IsFullScreen = false;
        }
    }

    private void ClearPendingOrientation()
    {
        _pendingOrientation = PendingOrientation.None;
        _pendingOrientationTimeoutTimer?.Dispose();
        _pendingOrientationTimeoutTimer = null;
    }

    /// <summary>Wired to <see cref="IAppLifecycleService.OrientationChanged"/> in the constructor.
    /// Marshals to the UI thread — safe now that slice 1 removed the ordering dependency on
    /// <c>EnsurePrimaryDestinationVisibleAsync</c>.</summary>
    private void OnOrientationChanged(bool isLandscape) =>
        _dispatcher.BeginInvokeOnMainThread(() => HandleOrientationChanged(isLandscape));

    private void HandleOrientationChanged(bool isLandscape)
    {
        if (_pendingOrientation == PendingOrientation.Landscape && isLandscape)
        {
            // The lock is NOT released here: OrientationChanged reports the *window's* orientation,
            // which flipped because this ViewModel asked it to, not because the user turned the
            // device. Releasing now would resolve Unspecified against a device that is still
            // physically portrait (or against the user's rotation lock with auto-rotate off) and
            // snap straight back, taking full screen with it. The lock is held for as long as full
            // screen is on and released the moment it ends (OnIsFullScreenChanged) — the same
            // behaviour YouTube has: button-entered full screen stays landscape until the user
            // exits it, rotation-entered full screen (no lock ever taken) still exits on rotation.
            ClearPendingOrientation();
            IsFullScreen = true;
            return;
        }

        if (_pendingOrientation == PendingOrientation.Portrait && !isLandscape)
        {
            // No explicit Release() here: this always transitions IsFullScreen true -> false, so
            // OnIsFullScreenChanged's release choke point already covers it — an explicit call
            // here would double-release.
            ClearPendingOrientation();
            IsFullScreen = false;
            return;
        }

        var compact = ViewerControlLayout.IsCompactDevice(_lifecycle.SmallestWidthDp);

        if (isLandscape && compact && IsPlaying && !IsFullScreen)
            IsFullScreen = true;
        else if (!isLandscape && compact && IsFullScreen)
            IsFullScreen = false;
    }

    /// <summary>Wired to <see cref="IAppLifecycleService.AppPaused"/> in the constructor: never
    /// leave the device pinned to an orientation, and never request one, while backgrounding.</summary>
    private void OnAppPaused() => ForceExitFullScreen();

    /// <summary>Unconditionally exits full screen and releases any orientation lock/pending
    /// request. Used when this ViewModel's host page is torn down
    /// (<see cref="Services.ViewerFullScreenChromeController.Detach"/>), the app is backgrounded
    /// (<see cref="OnAppPaused"/>), or this ViewModel is disposed. Never requests a rotation.</summary>
    public void ForceExitFullScreen()
    {
        ClearPendingOrientation();
        _orientationLock.Release();
        IsFullScreen = false;
    }

    /// <summary>Full screen's Back handling, in priority order: the PTZ layer, if open, closes and
    /// consumes the press; otherwise full screen exits (or, if already exiting, swallows a repeated
    /// press via <see cref="BeginExitFullScreen"/>'s own guard) and consumes; otherwise the press is
    /// not consumed.</summary>
    public bool HandleBackButtonPress()
    {
        if (IsPtzLayerVisible)
        {
            IsPtzLayerVisible = false;
            NotifyControlInteraction();
            return true;
        }

        if (IsFullScreen)
        {
            BeginExitFullScreen();
            return true;
        }

        return false;
    }
}
