using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
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
    /// <summary>
    /// True for the <see cref="ViewerView"/> embedded in <see cref="FullScreenViewerPage"/>,
    /// so it never itself presents a nested full-screen modal.
    /// </summary>
    public static readonly BindableProperty IsModalHostProperty =
        BindableProperty.Create(nameof(IsModalHost), typeof(bool), typeof(ViewerView), false);

    public bool IsModalHost
    {
        get => (bool)GetValue(IsModalHostProperty);
        set => SetValue(IsModalHostProperty, value);
    }

    // Rendering plumbing only (allowed in code-behind): a ~30 fps pull loop that
    // invalidates the canvas when the bridge has produced a newer frame, and a
    // paint handler that blits the ARGB int[] into a reusable SKBitmap.
    private IDispatcherTimer? _renderTimer;
    private NdiVideoFrame? _pendingFrame;
    private long _lastRenderedTimestamp = -1;
    private SKBitmap? _frameBitmap;

    private ViewerViewModel? _boundViewModel;
    private bool _presentingFullScreen;

    public ViewerView()
    {
        InitializeComponent();

        VideoCanvas.PaintSurface += OnPaintSurface;
        SizeChanged += (_, _) => UpdateLayoutVisibility();
    }

    /// <summary>Starts (or resumes) the ~30 fps frame pull loop. Idempotent.</summary>
    public void StartRendering()
    {
        if (_renderTimer is null)
        {
            _renderTimer = Dispatcher.CreateTimer();
            _renderTimer.Interval = TimeSpan.FromMilliseconds(33);
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
    /// Full teardown for a modal-host instance once its <see cref="FullScreenViewerPage"/> has
    /// been popped: releases the render timer, detaches from the bound ViewModel, clears
    /// BindingContext, and releases the frame bitmap.
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

        _frameBitmap?.Dispose();
        _frameBitmap = null;
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

        // IsFullScreen is shared VM state, so the donor instance sees it too; only the
        // modal host may show the full-screen overlay.
        Overlay.IsVisible = isFullScreen && IsModalHost;
        Deck.IsVisible = !isFullScreen && layout == ViewerControlLayoutKind.Deck;
        Sheet.IsVisible = !isFullScreen && layout == ViewerControlLayoutKind.Sheet;

        var innerHeight = isFullScreen ? Height : Height - (2 * ViewerControlLayout.ViewerContentPaddingDp);
        var videoHeight = ViewerControlLayout.ChooseVideoHeightDp(innerHeight, layout, isFullScreen);
        if (Math.Abs(VideoCanvas.HeightRequest - videoHeight) > 0.5)
            VideoCanvas.HeightRequest = videoHeight;
    }

    private async void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewerViewModel.IsFullScreen))
            UpdateLayoutVisibility();

        if (IsModalHost) return;
        if (e.PropertyName != nameof(ViewerViewModel.IsFullScreen)) return;
        if (BindingContext is not ViewerViewModel vm || !vm.IsFullScreen) return;
        if (_presentingFullScreen) return;

        _presentingFullScreen = true;
        try
        {
            await PresentFullScreenAsync(vm);
        }
        catch
        {
            vm.IsFullScreen = false;
            StartRendering();
        }
        finally
        {
            _presentingFullScreen = false;
        }
    }

    private async Task PresentFullScreenAsync(ViewerViewModel vm)
    {
        StopRendering(); // explicit hand-off — page lifecycle is not reliable under a modal push
        var factory = IPlatformApplication.Current?.Services.GetService<Func<FullScreenViewerPage>>();
        if (factory is null || Shell.Current is null)
        {
            StartRendering();
            return;
        }

        var page = factory();
        page.Initialize(vm, onClosed: StartRendering);
        await Shell.Current.Navigation.PushModalAsync(page);
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
