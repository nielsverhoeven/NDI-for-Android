using System.Runtime.InteropServices;
using Microsoft.Maui.Controls;
using NdiForAndroid.Features.Viewer.ViewModels;
using NdiForAndroid.NdiBridge;
using SkiaSharp;
using SkiaSharp.Views.Maui;

namespace NdiForAndroid.Features.Viewer.Views;

[QueryProperty(nameof(SourceId), "sourceId")]
public partial class ViewerPage : ContentPage
{
    private readonly ViewerViewModel _viewModel;

    // Rendering plumbing only (allowed in code-behind): a ~30 fps pull loop that
    // invalidates the canvas when the bridge has produced a newer frame, and a
    // paint handler that blits the ARGB int[] into a reusable SKBitmap.
    private IDispatcherTimer? _renderTimer;
    private NdiVideoFrame? _pendingFrame;
    private long _lastRenderedTimestamp = -1;
    private SKBitmap? _frameBitmap;

    public string? SourceId
    {
        set
        {
            if (_viewModel is not null)
                _viewModel.SourceId = value;
        }
    }

    public ViewerPage(ViewerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        VideoCanvas.PaintSurface += OnPaintSurface;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_renderTimer is null)
        {
            _renderTimer = Dispatcher.CreateTimer();
            _renderTimer.Interval = TimeSpan.FromMilliseconds(33);
            _renderTimer.Tick += OnRenderTick;
        }

        _renderTimer.Start();
    }

    protected override void OnDisappearing()
    {
        _renderTimer?.Stop();
        base.OnDisappearing();
    }

    private void OnRenderTick(object? sender, EventArgs e)
    {
        var frame = _viewModel.CurrentFrame;
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

        // Reuse the bitmap across frames; reallocate only on size change.
        if (_frameBitmap is null || _frameBitmap.Width != frame.Width || _frameBitmap.Height != frame.Height)
        {
            _frameBitmap?.Dispose();
            _frameBitmap = new SKBitmap(
                new SKImageInfo(frame.Width, frame.Height, SKColorType.Bgra8888, SKAlphaType.Opaque));
        }

        // On little-endian ARM, an ARGB int equals BGRA bytes in memory, which is
        // exactly SKColorType.Bgra8888 — a straight memcpy, no per-pixel conversion.
        Marshal.Copy(frame.ArgbPixels, 0, _frameBitmap.GetPixels(), frame.ArgbPixels.Length);

        // Letterbox: aspect-fit the frame into the canvas.
        var info = e.Info;
        float scale = Math.Min((float)info.Width / frame.Width, (float)info.Height / frame.Height);
        float w = frame.Width * scale;
        float h = frame.Height * scale;
        var dest = SKRect.Create((info.Width - w) / 2f, (info.Height - h) / 2f, w, h);

        canvas.DrawBitmap(_frameBitmap, dest);
    }
}
