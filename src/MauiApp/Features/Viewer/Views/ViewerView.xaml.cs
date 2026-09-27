using System.ComponentModel;
using System.Runtime.InteropServices;
using NdiForAndroid.Features.Viewer;
using NdiForAndroid.Features.Viewer.ViewModels;
using NdiForAndroid.NdiBridge;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace NdiForAndroid.Features.Viewer.Views;

/// <summary>
/// Reusable NDI viewer surface (video canvas + playback controls). Assumes a
/// <see cref="ViewerViewModel"/> BindingContext (inherited or set explicitly).
/// The host page drives the render loop via <see cref="StartRendering"/> /
/// <see cref="StopRendering"/> from its OnAppearing/OnDisappearing.
/// </summary>
public partial class ViewerView : ContentView
{
    // Rendering plumbing only (allowed in code-behind): frames are presented on arrival — the
    // ViewModel raises FrameReady on the UI thread, already coalesced — with a ~30 fps timer kept
    // as a fallback so a hole in the event wiring degrades to the old behaviour rather than to a
    // blank canvas (#416). The paint handler draws the bridge's ARGB int[] in place (pinned, no
    // intermediate bitmap copy).
    private const int FallbackPollIntervalMs = 33;

    private IDispatcherTimer? _renderTimer;
    private NdiVideoFrame? _pendingFrame;
    private long _lastRenderedTimestamp = -1;
    private bool _isRenderingActive;

    // True when the next paint is one a newly arrived frame caused; only such a paint is measured
    // (OnPaintSurface also runs for relayout/rotation/theme repaints of a stale frame).
    private bool _frameAwaitingReport;

    private ViewerViewModel? _boundViewModel;

    public ViewerView()
    {
        InitializeComponent();

        VideoCanvas.PaintSurface += OnPaintSurface;
        SizeChanged += (_, _) => UpdateLayoutVisibility();
    }

    /// <summary>Enables presentation: draw-on-arrival plus the ~30 fps fallback pull. Idempotent.
    /// Also opens the ViewModel's pump-side gate, so the pump stops posting to a View that is not
    /// rendering.</summary>
    public void StartRendering()
    {
        _isRenderingActive = true;
        _boundViewModel?.SetRenderingActive(true);

        if (_renderTimer is null)
        {
            _renderTimer = Dispatcher.CreateTimer();
            _renderTimer.Interval = TimeSpan.FromMilliseconds(FallbackPollIntervalMs);
            _renderTimer.Tick += OnRenderTick;
        }

        _renderTimer.Start();
    }

    /// <summary>Disables presentation. Safe to call when not rendering. Closes both gates: the
    /// ViewModel stops posting per-frame invalidates and the two local triggers stop presenting.</summary>
    public void StopRendering()
    {
        _isRenderingActive = false;
        _frameAwaitingReport = false;
        _boundViewModel?.SetRenderingActive(false);
        _renderTimer?.Stop();
    }

    /// <summary>
    /// Full teardown once the host page showing this instance has actually left the nav stack:
    /// releases the render timer, detaches from the bound ViewModel and clears BindingContext.
    /// </summary>
    public void Teardown()
    {
        if (_renderTimer is not null)
        {
            _renderTimer.Stop();
            _renderTimer.Tick -= OnRenderTick;
            _renderTimer = null;
        }

        _isRenderingActive = false;
        _frameAwaitingReport = false;

        if (_boundViewModel is not null)
        {
            _boundViewModel.SetRenderingActive(false);
            _boundViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _boundViewModel.FrameReady -= OnFrameReady;
            _boundViewModel = null;
        }

        BindingContext = null;
        _pendingFrame = null;
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (_boundViewModel is not null)
        {
            // A ViewModel this View no longer presents must not be left with its gate open — the
            // Expanded pane's PaneViewer is a never-disposed singleton and would keep posting.
            _boundViewModel.SetRenderingActive(false);
            _boundViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _boundViewModel.FrameReady -= OnFrameReady;
        }

        _boundViewModel = BindingContext as ViewerViewModel;

        if (_boundViewModel is not null)
        {
            _boundViewModel.PropertyChanged += OnViewModelPropertyChanged;
            _boundViewModel.FrameReady += OnFrameReady;
            // Re-assert the host's current state: {Binding PaneViewer} resolves only on the first
            // Expanded "Watch" tap, long after StartRendering() ran.
            _boundViewModel.SetRenderingActive(_isRenderingActive);
        }

        UpdateLayoutVisibility();
    }

    private void UpdateLayoutVisibility()
    {
        var isFullScreen = _boundViewModel?.IsFullScreen ?? false;
        var layout = ViewerControlLayout.Choose(Width, Height);

        Overlay.IsVisible = isFullScreen;
        Deck.IsVisible = !isFullScreen && layout == ViewerControlLayoutKind.Deck;
        Sheet.IsVisible = !isFullScreen && layout == ViewerControlLayoutKind.Sheet;

        var innerHeight = isFullScreen ? Height : Height - (2 * ViewerControlLayout.ViewerContentPaddingDp);
        var videoHeight = ViewerControlLayout.ChooseVideoHeightDp(innerHeight, layout, isFullScreen);
        if (Math.Abs(VideoCanvas.HeightRequest - videoHeight) > 0.5)
            VideoCanvas.HeightRequest = videoHeight;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewerViewModel.IsFullScreen))
            UpdateLayoutVisibility();

        // #348: Stop() sets IsStopped, but the render loop only repaints on a *new* frame
        // timestamp — without this the last live frame would stay painted forever underneath
        // the "Stopped" overlay. Clearing here (not in the ViewModel) keeps SkiaSharp/bitmap
        // concerns in the View's rendering plumbing, matching OnRenderTick/OnPaintSurface below.
        if (e.PropertyName == nameof(ViewerViewModel.IsStopped) &&
            sender is ViewerViewModel { IsStopped: true })
        {
            _pendingFrame = null;
            _lastRenderedTimestamp = -1;
            _frameAwaitingReport = false;
            VideoCanvas.InvalidateSurface();
        }
    }

    /// <summary>Draw-on-arrival (#416). Already on the UI thread and already coalesced by the
    /// ViewModel, so this is a single presentation attempt — never a loop and never a dispatch.</summary>
    private void OnFrameReady(object? sender, EventArgs e) => PresentLatestFrame();

    /// <summary>Fallback pull. Once the event path is current this costs one property read and one
    /// long comparison per tick, because the timestamp dedupe below short-circuits it.</summary>
    private void OnRenderTick(object? sender, EventArgs e) => PresentLatestFrame();

    private void PresentLatestFrame()
    {
        if (!_isRenderingActive || _boundViewModel is null)
            return;

        // Second layer over the bridge's prologue blank: Stop / CancelRetry / FailReconnect all
        // mean "do not paint", and this also covers the one case the bridge cannot — a pane
        // retired by ReleaseIfDisowned, where no stop happened at all because the frames now
        // belong to another ViewerViewModel and must keep flowing for it. Without this, that
        // pane blanks for a single frame on IsStopped and then repaints the new owner's live
        // video under its own "Stopped" badge.
        if (_boundViewModel.IsStopped)
            return;

        var frame = _boundViewModel.CurrentFrame;
        if (frame is null || frame.CapturedAtEpochMillis == _lastRenderedTimestamp)
            return;

        _lastRenderedTimestamp = frame.CapturedAtEpochMillis;
        _pendingFrame = frame;
        _frameAwaitingReport = true;
        VideoCanvas.InvalidateSurface();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Black);

        // Captured and cleared before the early return below, so a paint that draws nothing does
        // not leave the flag armed for a later, frame-less repaint.
        var isNewFrame = _frameAwaitingReport;
        _frameAwaitingReport = false;

        var frame = _pendingFrame;
        if (frame is null || frame.Width <= 0 || frame.Height <= 0)
            return;

        if (frame.ArgbPixels.Length < frame.Width * frame.Height)
            return;

        // Letterbox: aspect-fit the frame into the canvas.
        var info = e.Info;
        float scale = Math.Min((float)info.Width / frame.Width, (float)info.Height / frame.Height);
        float w = frame.Width * scale;
        float h = frame.Height * scale;
        var dest = SKRect.Create((info.Width - w) / 2f, (info.Height - h) / 2f, w, h);

        // On little-endian ARM, an ARGB int equals BGRA bytes in memory, which is exactly
        // SKColorType.Bgra8888 — so Skia can read the bridge's array directly. Pin it for the
        // duration of the draw and wrap it as a zero-copy image instead of first memcpy-ing
        // ~8 MB (1080p) into an intermediate bitmap on the UI thread every frame. The bridge's
        // triple buffer guarantees this array is not rewritten while the draw is in progress.
        var handle = GCHandle.Alloc(frame.ArgbPixels, GCHandleType.Pinned);
        try
        {
            var imageInfo = new SKImageInfo(frame.Width, frame.Height, SKColorType.Bgra8888, SKAlphaType.Opaque);
            using var pixmap = new SKPixmap(imageInfo, handle.AddrOfPinnedObject(), imageInfo.RowBytes);
            using var image = SKImage.FromPixels(pixmap);
            if (image is null)
                return;

            // Default sampling (nearest neighbour, no mipmaps) — the cheapest scaler, as before.
            canvas.DrawImage(image, dest, SKSamplingOptions.Default);
        }
        finally
        {
            handle.Free();
        }

        // Only the first paint of a new frame is measured — a relayout/rotation repaint of a stale
        // frame would otherwise report an inflated latency.
        if (isNewFrame)
        {
            _boundViewModel?.ReportFrameDrawn(
                frame.ReceivedAtTickMillis, frame.CapturedAtEpochMillis, frame.TimestampIsSynthesized);
        }
    }
}
