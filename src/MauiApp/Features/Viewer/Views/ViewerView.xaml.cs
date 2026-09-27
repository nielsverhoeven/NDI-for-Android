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
    // Rendering plumbing only (allowed in code-behind): a pull loop that invalidates the
    // canvas as soon as the bridge has produced a newer frame, and a paint handler that
    // draws the bridge's ARGB int[] in place (pinned, no intermediate bitmap copy).
    //
    // The poll runs at ~120 Hz rather than the source frame rate: the old 33 ms tick could
    // leave a fresh frame waiting up to 33 ms before it was even invalidated and showed at
    // most every other frame of a 60 fps source. A tick with no new frame is a single
    // reference compare, so polling faster costs next to nothing.
    private const int RenderPollIntervalMs = 8;

    private IDispatcherTimer? _renderTimer;
    private NdiVideoFrame? _pendingFrame;
    private long _lastRenderedTimestamp = -1;

    private ViewerViewModel? _boundViewModel;

    public ViewerView()
    {
        InitializeComponent();

        VideoCanvas.PaintSurface += OnPaintSurface;
        SizeChanged += (_, _) => UpdateLayoutVisibility();
    }

    /// <summary>Starts (or resumes) the frame pull loop. Idempotent.</summary>
    public void StartRendering()
    {
        if (_renderTimer is null)
        {
            _renderTimer = Dispatcher.CreateTimer();
            _renderTimer.Interval = TimeSpan.FromMilliseconds(RenderPollIntervalMs);
            _renderTimer.Tick += OnRenderTick;
        }

        _renderTimer.Start();
    }

    /// <summary>Stops the frame pull loop. Safe to call when not rendering.</summary>
    public void StopRendering()
    {
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

        if (_boundViewModel is not null)
        {
            _boundViewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _boundViewModel = null;
        }

        BindingContext = null;
        _pendingFrame = null;
    }

    protected override void OnBindingContextChanged()
    {
        base.OnBindingContextChanged();

        if (_boundViewModel is not null)
            _boundViewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _boundViewModel = BindingContext as ViewerViewModel;

        if (_boundViewModel is not null)
            _boundViewModel.PropertyChanged += OnViewModelPropertyChanged;

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
            VideoCanvas.InvalidateSurface();
        }
    }

    private void OnRenderTick(object? sender, EventArgs e)
    {
        if (BindingContext is not ViewerViewModel viewModel)
            return;

        var frame = viewModel.CurrentFrame;
        if (frame is null || frame.CapturedAtEpochMillis == _lastRenderedTimestamp)
            return;

        _lastRenderedTimestamp = frame.CapturedAtEpochMillis;
        _pendingFrame = frame;
        VideoCanvas.InvalidateSurface();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Black);

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
    }
}
