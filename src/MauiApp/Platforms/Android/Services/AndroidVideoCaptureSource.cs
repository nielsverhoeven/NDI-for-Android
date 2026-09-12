using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Android.Hardware.Display;
using Android.Media;
using Android.Media.Projection;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Microsoft.Maui.ApplicationModel;
using NdiForAndroid.Services;
using System.Runtime.InteropServices;
using AImage = Android.Media.Image;
using OperationCanceledException = System.OperationCanceledException;

namespace NdiForAndroid.Platforms.Android.Services;

/// <summary>
/// Android video capture for NDI send: screen via MediaProjection + ImageReader
/// (RGBA_8888) and cameras via Camera2 + ImageReader (YUV_420_888 repacked to NV12).
/// Frames are raised on a dedicated capture <see cref="HandlerThread"/> — never the
/// UI thread — and reuse producer-owned buffers per the <see cref="IVideoCaptureSource"/>
/// contract (consumers finish with the buffer before the event handler returns).
/// Camera frames are rotated (Nv12FrameRotator) by CameraFrameOrientation(SENSOR_ORIENTATION,
/// display rotation, lens facing) so receivers get an upright picture; portrait devices
/// therefore send height×width frames. The display rotation comes from DisplayManager
/// (registered on the capture thread), not from MAUI's DeviceDisplay — see the field remarks.
/// </summary>
public sealed class AndroidVideoCaptureSource : IVideoCaptureSource
{
    /// <summary>logcat tag for capture-path diagnostics (`adb logcat -s NDI-Capture`).</summary>
    private const string LogTag = "NDI-Capture";

    /// <summary>Activity-result request code for the MediaProjection consent dialog.</summary>
    internal const int ScreenCaptureRequestCode = 42731;

    private const int MaxScreenLongEdge = 1280; // Wi-Fi bandwidth guidance: clamp long edge
    private const int MaxCameraWidth = 1280;
    private const int MaxCameraHeight = 720;

    /// <summary>Completed by <see cref="HandleScreenCaptureResult"/> when MainActivity receives the consent result.</summary>
    private static TaskCompletionSource<(Result ResultCode, Intent? Data)>? _consentTcs;

    private readonly IScreenSharePlatformService _screenShareService;
    private readonly SemaphoreSlim _stopLock = new(1, 1);

    private HandlerThread? _handlerThread;
    private ImageReader? _imageReader;

    // Screen capture state.
    private MediaProjection? _projection;
    private VirtualDisplay? _virtualDisplay;
    private MediaProjection.Callback? _projectionCallback;
    private bool _startedForegroundSession;

    // Camera capture state.
    private CameraDevice? _cameraDevice;
    private CameraCaptureSession? _captureSession;

    // Camera orientation compensation. Sensor orientation and lens facing are fixed for
    // the opened camera; the display rotation is read at start and kept current by a
    // DisplayManager.IDisplayListener whose callbacks are delivered on the capture HandlerThread —
    // the same thread that reads the field per frame.
    // DeviceDisplay.MainDisplayInfoChanged is deliberately NOT used: MAUI's Android change
    // detection hangs off an OrientationEventListener (accelerometer), so it never fires when the
    // rotation changes without device motion (auto-rotate off + forced user_rotation), and it
    // depends on sensor delivery to a possibly-backgrounded activity while capture runs from a
    // foreground service. Device-verified 2026-09-12: zero events across four forced rotations.
    private const int DefaultDisplayId = 0; // Android.Views.Display.DEFAULT_DISPLAY

    private int _sensorOrientationDegrees;
    private bool _isFrontFacing;
    private volatile int _displayRotationDegrees;
    private DisplayManager? _displayManager;
    private DisplayRotationListener? _displayRotationListener;

    // Producer-owned reusable buffers (see class remarks).
    private byte[]? _frameBuffer;
    private byte[]? _rotatedBuffer;
    private byte[]? _chromaScratchU;
    private byte[]? _chromaScratchV;

    private volatile bool _isActive;

    public AndroidVideoCaptureSource(IScreenSharePlatformService screenShareService)
    {
        _screenShareService = screenShareService;
    }

    public event EventHandler<CapturedVideoFrame>? FrameReady;

    public event EventHandler<CaptureStoppedEventArgs>? Stopped;

    public bool IsActive => _isActive;

    private void RaiseStopped(CaptureStopReason reason, string? message = null)
    {
        if (!_isActive)
            return;

        try { Stopped?.Invoke(this, new CaptureStoppedEventArgs(reason, message)); }
        catch { /* subscriber failures must never break capture teardown */ }
    }

    /// <summary>
    /// Called by <c>MainActivity.OnActivityResult</c> to complete the pending
    /// screen-capture consent flow started by <see cref="StartAsync"/>.
    /// </summary>
    public static void HandleScreenCaptureResult(Result resultCode, Intent? data) =>
        _consentTcs?.TrySetResult((resultCode, data));

    public async Task StartAsync(VideoInputKind kind, CancellationToken cancellationToken = default)
    {
        if (_isActive)
            throw new InvalidOperationException("Video capture is already active; call StopAsync first.");

        try
        {
            if (kind == VideoInputKind.Screen)
                await StartScreenAsync(cancellationToken).ConfigureAwait(false);
            else
                await StartCameraAsync(kind, cancellationToken).ConfigureAwait(false);

            _isActive = true;
        }
        catch
        {
            // Roll back any partially-created native state before surfacing the failure.
            await StopAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task StopAsync()
    {
        await _stopLock.WaitAsync().ConfigureAwait(false);
        try
        {
            _isActive = false;

            // Camera teardown (no-ops for screen capture).
            try { _captureSession?.StopRepeating(); } catch { /* session may already be closed */ }
            try { _captureSession?.Close(); } catch { /* ignore — best-effort teardown */ }
            _captureSession = null;
            try { _cameraDevice?.Close(); } catch { /* ignore — best-effort teardown */ }
            _cameraDevice = null;

            StopDisplayRotationTracking();

            // Screen teardown (no-ops for camera capture).
            try { _virtualDisplay?.Release(); } catch { /* ignore — best-effort teardown */ }
            _virtualDisplay = null;
            if (_projection is { } projection)
            {
                try
                {
                    if (_projectionCallback is { } callback)
                        projection.UnregisterCallback(callback);
                }
                catch { /* ignore — best-effort teardown */ }
                try { projection.Stop(); } catch { /* ignore — best-effort teardown */ }
            }
            _projection = null;
            _projectionCallback = null;

            try { _imageReader?.Close(); } catch { /* ignore — best-effort teardown */ }
            _imageReader = null;

            try { _handlerThread?.QuitSafely(); } catch { /* ignore — best-effort teardown */ }
            _handlerThread = null;

            if (_startedForegroundSession)
            {
                _startedForegroundSession = false;
                try
                {
                    await _screenShareService.StopForegroundSessionAsync().ConfigureAwait(false);
                }
                catch { /* foreground-service stop must never fault teardown */ }
            }
        }
        finally
        {
            _stopLock.Release();
        }
    }

    // ---------------------------------------------------------------- screen

    private async Task StartScreenAsync(CancellationToken cancellationToken)
    {
        var activity = Platform.CurrentActivity
            ?? throw new InvalidOperationException("No current activity available for the screen-capture consent dialog.");
        var manager = (MediaProjectionManager?)activity.GetSystemService(Context.MediaProjectionService)
            ?? throw new InvalidOperationException("MediaProjectionManager is unavailable.");

        // 1. Consent FIRST (API 34 ordering: consent → FGS running → GetMediaProjection).
        var consent = new TaskCompletionSource<(Result ResultCode, Intent? Data)>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        _consentTcs = consent;
        await MainThread.InvokeOnMainThreadAsync(() =>
            activity.StartActivityForResult(manager.CreateScreenCaptureIntent(), ScreenCaptureRequestCode))
            .ConfigureAwait(false);

        Result resultCode;
        Intent? data;
        try
        {
            (resultCode, data) = await consent.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            Interlocked.CompareExchange(ref _consentTcs, null, consent);
        }

        if (resultCode != Result.Ok || data is null)
            throw new OperationCanceledException("Screen capture consent was declined.");

        // 2. The mediaProjection-typed foreground service must be RUNNING before
        //    GetMediaProjection on API 34+.
        await _screenShareService.StartForegroundSessionAsync("NDI-Android", VideoInputKind.Screen, cancellationToken).ConfigureAwait(false);
        _startedForegroundSession = true;

        // StartForegroundService completes asynchronously; until the service has
        // actually reached the foreground, GetMediaProjection throws SecurityException
        // on API 34+. Retry briefly instead of racing it.
        MediaProjection? projection = null;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                projection = manager.GetMediaProjection((int)resultCode, data);
            }
            catch (Java.Lang.SecurityException) when (attempt < 19)
            {
                // Foreground service not up yet — fall through to the delay below.
            }

            if (projection is not null)
                break;

            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
        }

        if (projection is null)
            throw new InvalidOperationException("MediaProjection could not be acquired after consent.");

        var handler = StartCaptureThread();

        // 3. Register the projection callback BEFORE createVirtualDisplay (required on API 34+).
        var callback = new ProjectionCallback(this);
        projection.RegisterCallback(callback, handler);
        _projection = projection;
        _projectionCallback = callback;

        var (width, height) = GetScreenCaptureSize();
        var densityDpi = (int)Math.Round(Microsoft.Maui.Devices.DeviceDisplay.MainDisplayInfo.Density * 160);
        if (densityDpi <= 0)
            densityDpi = 160; // DisplayMetrics.DENSITY_DEFAULT

        // ImageFormatType has no RGBA_8888 member; screen capture uses
        // PixelFormat.RGBA_8888 (0x1 == Android.Graphics.Format.Rgba8888), hence the cast.
        var reader = ImageReader.NewInstance(width, height, (ImageFormatType)Format.Rgba8888, 2 /* maxImages */);
        reader.SetOnImageAvailableListener(new ImageListener(OnScreenImage), handler);
        _imageReader = reader;

        var surface = reader.Surface
            ?? throw new InvalidOperationException("ImageReader surface is unavailable.");

        // VirtualDisplayFlags is the semantically correct flag set; the binding's
        // parameter type is DisplayFlags due to Mono.Android enumification, hence the cast.
        _virtualDisplay = projection.CreateVirtualDisplay(
            "ndi-screen", width, height, densityDpi,
            (DisplayFlags)VirtualDisplayFlags.AutoMirror,
            surface, null, handler);
    }

    private static (int Width, int Height) GetScreenCaptureSize()
    {
        var info = Microsoft.Maui.Devices.DeviceDisplay.MainDisplayInfo;
        var width = (int)info.Width;
        var height = (int)info.Height;
        if (width <= 0 || height <= 0)
            (width, height) = (1280, 720);

        var longEdge = Math.Max(width, height);
        if (longEdge > MaxScreenLongEdge)
        {
            var scale = (double)MaxScreenLongEdge / longEdge;
            width = (int)Math.Round(width * scale);
            height = (int)Math.Round(height * scale);
        }

        // Even dimensions keep NDI/encoder consumers happy.
        return (Math.Max(2, width & ~1), Math.Max(2, height & ~1));
    }

    private void OnScreenImage(ImageReader reader)
    {
        AImage? image = null;
        try
        {
            image = reader.AcquireLatestImage();
            if (image is null)
                return;

            var plane = image.GetPlanes() is { Length: > 0 } planes ? planes[0] : null;
            if (plane?.Buffer is not { } buffer)
                return;

            var width = image.Width;
            var height = image.Height;
            var tightStride = width * 4;
            var required = tightStride * height;

            var frame = _frameBuffer;
            if (frame is null || frame.Length < required)
                _frameBuffer = frame = new byte[required];

            var source = JNIEnv.GetDirectBufferAddress(buffer.Handle);
            if (source == IntPtr.Zero)
                return; // ImageReader buffers are direct; drop the frame if not.

            var rowStride = plane.RowStride;
            if (rowStride == tightStride)
            {
                Marshal.Copy(source, frame, 0, required);
            }
            else
            {
                // Compact the padded rows into a tight RGBA buffer.
                for (var row = 0; row < height; row++)
                    Marshal.Copy(source + row * rowStride, frame, row * tightStride, tightStride);
            }

            FrameReady?.Invoke(this, new CapturedVideoFrame(
                width, height, CapturedPixelFormat.Rgba32, frame, tightStride, 30));
        }
        catch
        {
            // Capture callbacks run on a native handler thread — never crash the process.
        }
        finally
        {
            try { image?.Close(); } catch { /* ignore — image slot is reclaimed on reader close */ }
        }
    }

    // ---------------------------------------------------------------- camera

    private async Task StartCameraAsync(VideoInputKind kind, CancellationToken cancellationToken)
    {
        // MAUI requires permission requests to originate on the main thread.
        var status = await MainThread.InvokeOnMainThreadAsync(
            () => Permissions.RequestAsync<Permissions.Camera>()).ConfigureAwait(false);
        if (status != PermissionStatus.Granted)
            throw new OperationCanceledException("Camera permission was denied.");
        cancellationToken.ThrowIfCancellationRequested();

        await _screenShareService.StartForegroundSessionAsync("NDI-Android", kind, cancellationToken).ConfigureAwait(false);
        _startedForegroundSession = true;

        var context = global::Android.App.Application.Context;
        var manager = (CameraManager?)context.GetSystemService(Context.CameraService)
            ?? throw new InvalidOperationException("CameraManager is unavailable.");

        var facing = kind == VideoInputKind.CameraFront ? LensFacing.Front : LensFacing.Back;
        var (cameraId, characteristics) = FindCamera(manager, facing);
        var (width, height) = PickCameraSize(characteristics);

        // Orientation compensation: SENSOR_ORIENTATION is fixed per camera; the display
        // rotation is read from DisplayManager now and tracked from the same API while capturing,
        // so the initial value and the updates can never disagree. Must run after the capture
        // thread exists — its Handler is where the display callbacks are delivered.
        _isFrontFacing = facing == LensFacing.Front;
        _sensorOrientationDegrees = ReadSensorOrientation(characteristics);

        var handler = StartCaptureThread();
        ArmDisplayRotationTracking(context, handler);

        var reader = ImageReader.NewInstance(width, height, ImageFormatType.Yuv420888, 2 /* maxImages */);
        reader.SetOnImageAvailableListener(new ImageListener(OnCameraImage), handler);
        _imageReader = reader;
        var surface = reader.Surface
            ?? throw new InvalidOperationException("ImageReader surface is unavailable.");

        var opened = new TaskCompletionSource<CameraDevice>(TaskCreationOptions.RunContinuationsAsynchronously);
        manager.OpenCamera(cameraId, new CameraStateCallback(this, opened), handler);
        var camera = await opened.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        _cameraDevice = camera;

        var configured = new TaskCompletionSource<CameraCaptureSession>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stateCallback = new SessionStateCallback(configured);
        var useLegacySession = !OperatingSystem.IsAndroidVersionAtLeast(28);
        var apiLevel = (int)global::Android.OS.Build.VERSION.SdkInt;
        if (!useLegacySession)
        {
            // API 28+: SessionConfiguration + Executor. HandlerExecutor keeps the state callbacks
            // on the capture HandlerThread, exactly like the Handler overload did. Any failure here
            // (e.g. an OEM quirk in the modern path) falls back to the legacy overload below rather
            // than risk a broken camera session — see #284 safety note.
            try
            {
                var outputs = new List<OutputConfiguration> { new OutputConfiguration(surface) };
                var sessionConfiguration = new SessionConfiguration(
                    (int)SessionType.Regular, outputs, new HandlerExecutor(handler), stateCallback);
                camera.CreateCaptureSession(sessionConfiguration);
                global::Android.Util.Log.Info(LogTag,
                    $"Capture session requested via SessionConfiguration (API {apiLevel}).");
            }
            catch (Exception ex)
            {
                global::Android.Util.Log.Warn(LogTag,
                    $"SessionConfiguration path failed ({ex.GetType().Name}: {ex.Message}); falling back to the legacy CreateCaptureSession overload.");
                useLegacySession = true;
            }
        }
        if (useLegacySession)
        {
            // API 26-27 (minSdk 26): SessionConfiguration does not exist yet there. The Handler
            // overload is obsoleted on API 30 only, so it remains the correct call on these two
            // levels, and is also the safety fallback if the SessionConfiguration path above throws.
#pragma warning disable CA1422
            camera.CreateCaptureSession(new List<Surface> { surface }, stateCallback, handler);
#pragma warning restore CA1422
            global::Android.Util.Log.Info(LogTag,
                $"Capture session requested via the legacy CreateCaptureSession(List<Surface>) overload (API {apiLevel}).");
        }
        var session = await configured.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        _captureSession = session;

        var requestBuilder = camera.CreateCaptureRequest(CameraTemplate.Preview)
            ?? throw new InvalidOperationException("Failed to create a camera capture request.");
        requestBuilder.AddTarget(surface);
        var request = requestBuilder.Build()
            ?? throw new InvalidOperationException("Failed to build the camera capture request.");
        session.SetRepeatingRequest(request, null, handler);
    }

    private static (string Id, CameraCharacteristics Characteristics) FindCamera(CameraManager manager, LensFacing facing)
    {
        var lensKey = CameraCharacteristics.LensFacing;
        if (lensKey is null)
            throw new InvalidOperationException("CameraCharacteristics.LensFacing key is unavailable.");

        foreach (var id in manager.GetCameraIdList() ?? Array.Empty<string>())
        {
            if (manager.GetCameraCharacteristics(id) is not { } characteristics)
                continue;

            if (characteristics.Get(lensKey) is Java.Lang.Integer lens && lens.IntValue() == (int)facing)
                return (id, characteristics);
        }

        throw new InvalidOperationException($"No {facing} camera is available on this device.");
    }

    private static (int Width, int Height) PickCameraSize(CameraCharacteristics characteristics)
    {
        var mapKey = CameraCharacteristics.ScalerStreamConfigurationMap;
        var map = mapKey is null ? null : characteristics.Get(mapKey) as StreamConfigurationMap;
        var sizes = map?.GetOutputSizes((int)ImageFormatType.Yuv420888);

        // Largest YUV_420_888 size within 1280x720 (landscape); if every mode is
        // larger, fall back to the smallest available so capture still works.
        var best = (Width: 0, Height: 0);
        var smallest = (Width: 0, Height: 0);
        if (sizes is not null)
        {
            foreach (var size in sizes)
            {
                int w = size.Width, h = size.Height;
                if (w <= 0 || h <= 0)
                    continue;
                if (smallest.Width == 0 || (long)w * h < (long)smallest.Width * smallest.Height)
                    smallest = (w, h);
                if (w <= MaxCameraWidth && h <= MaxCameraHeight && (long)w * h > (long)best.Width * best.Height)
                    best = (w, h);
            }
        }

        var chosen = best.Width > 0 ? best
            : smallest.Width > 0 ? smallest
            : (Width: MaxCameraWidth, Height: MaxCameraHeight);

        // NV12 requires even dimensions.
        return (Math.Max(2, chosen.Width & ~1), Math.Max(2, chosen.Height & ~1));
    }

    private static int ReadSensorOrientation(CameraCharacteristics characteristics)
    {
        var key = CameraCharacteristics.SensorOrientation;
        return key is not null && characteristics.Get(key) is Java.Lang.Integer value ? value.IntValue() : 0;
    }

    /// <summary>
    /// Reads the current default-display rotation and tracks it for the lifetime of this capture
    /// session. Both the initial read and every update come from the same DisplayManager/Display
    /// API, so "armed" and "changed" can never disagree. Never throws: a host with no display
    /// service (headless service process) simply keeps the last known rotation and logs it.
    /// </summary>
    private void ArmDisplayRotationTracking(Context context, Handler handler)
    {
        try
        {
            _displayManager = (DisplayManager?)context.GetSystemService(Context.DisplayService);
            _displayRotationDegrees = ReadDisplayRotationDegrees();

            if (_displayManager is { } manager)
            {
                var listener = new DisplayRotationListener(this);
                // Callbacks arrive on the capture HandlerThread — the same thread that reads
                // _displayRotationDegrees in OnCameraImage — so an update can never land mid-frame
                // and the tracking keeps working while the activity is backgrounded behind the
                // foreground service.
                manager.RegisterDisplayListener(listener, handler);
                _displayRotationListener = listener;
            }

            global::Android.Util.Log.Info(LogTag,
                $"Orientation compensation armed: sensor {_sensorOrientationDegrees} deg, display rotation {_displayRotationDegrees} deg, front={_isFrontFacing}, tracking={_displayRotationListener is not null}.");
        }
        catch (Exception ex)
        {
            // Orientation tracking is best-effort: a failure here must not fail the capture start.
            global::Android.Util.Log.Warn(LogTag,
                $"Display rotation tracking unavailable ({ex.GetType().Name}: {ex.Message}); staying at {_displayRotationDegrees} deg for this session.");
        }
    }

    /// <summary>
    /// Current default-display rotation in degrees, or the last known value when the display is
    /// unavailable (DisplayManager or Display can be null on a headless host).
    /// </summary>
    private int ReadDisplayRotationDegrees()
    {
        var display = _displayManager?.GetDisplay(DefaultDisplayId);
        return display is null ? _displayRotationDegrees : ToDegrees((int)display.Rotation);
    }

    /// <summary>
    /// Unregisters the display listener. Called from StopAsync before the capture HandlerThread is
    /// quit, and safe to call when tracking was never armed (start rolls back through StopAsync).
    /// The listener is not disposed: a callback may be in flight on the capture thread.
    /// </summary>
    private void StopDisplayRotationTracking()
    {
        var manager = _displayManager;
        var listener = _displayRotationListener;
        _displayManager = null;
        _displayRotationListener = null;

        if (manager is null || listener is null)
            return;

        try { manager.UnregisterDisplayListener(listener); }
        catch { /* ignore — best-effort teardown */ }
    }

    /// <summary>Surface.ROTATION_* (0/1/2/3) → degrees; the cast at the call site keeps this independent of the binding's enum type.</summary>
    private static int ToDegrees(int surfaceRotation) => surfaceRotation switch
    {
        1 => 90,   // Surface.ROTATION_90
        2 => 180,  // Surface.ROTATION_180
        3 => 270,  // Surface.ROTATION_270
        _ => 0,    // Surface.ROTATION_0 / unknown
    };

    /// <summary>
    /// Capture HandlerThread (the Handler passed to RegisterDisplayListener); the same thread reads
    /// the field per frame in OnCameraImage. The field stays volatile because StartCameraAsync
    /// writes the initial value from the caller's thread.
    /// </summary>
    private void OnDisplayRotationChanged(int displayId)
    {
        if (displayId != DefaultDisplayId)
            return;

        var degrees = ReadDisplayRotationDegrees();
        if (degrees == _displayRotationDegrees)
            return; // OnDisplayChanged also fires for refresh-rate/HDR/brightness changes.

        _displayRotationDegrees = degrees;
        global::Android.Util.Log.Info(LogTag, $"Display rotation -> {degrees} deg.");
    }

    private void OnCameraImage(ImageReader reader)
    {
        AImage? image = null;
        try
        {
            image = reader.AcquireLatestImage();
            if (image is null)
                return;

            var planes = image.GetPlanes();
            if (planes is not { Length: 3 })
                return;

            var width = image.Width;
            var height = image.Height;
            var required = width * height * 3 / 2;

            var frame = _frameBuffer;
            if (frame is null || frame.Length < required)
                _frameBuffer = frame = new byte[required];

            if (!CopyLumaPlane(planes[0], width, height, frame))
                return;
            if (!RepackChromaToNv12(planes[1], planes[2], width, height, frame))
                return;

            var rotation = CameraFrameOrientation.ComputeRotationDegrees(
                _sensorOrientationDegrees, _displayRotationDegrees, _isFrontFacing);
            if (rotation == 0)
            {
                FrameReady?.Invoke(this, new CapturedVideoFrame(
                    width, height, CapturedPixelFormat.Nv12, frame, width, 30));
                return;
            }

            // Rotate into the second producer-owned buffer; portrait frames come out as
            // height×width (e.g. 720x1280) and the bridge derives aspect/stride per frame.
            var rotated = _rotatedBuffer;
            if (rotated is null || rotated.Length < required)
                _rotatedBuffer = rotated = new byte[required];
            var (outWidth, outHeight) = Nv12FrameRotator.Rotate(frame, width, height, rotation, rotated);
            FrameReady?.Invoke(this, new CapturedVideoFrame(
                outWidth, outHeight, CapturedPixelFormat.Nv12, rotated, outWidth, 30));
        }
        catch
        {
            // Capture callbacks run on a native handler thread — never crash the process.
        }
        finally
        {
            try { image?.Close(); } catch { /* ignore — image slot is reclaimed on reader close */ }
        }
    }

    private static bool CopyLumaPlane(AImage.Plane plane, int width, int height, byte[] dst)
    {
        if (plane.Buffer is not { } buffer)
            return false;

        var source = JNIEnv.GetDirectBufferAddress(buffer.Handle);
        if (source == IntPtr.Zero)
            return false;

        var rowStride = plane.RowStride;
        if (rowStride == width)
        {
            Marshal.Copy(source, dst, 0, width * height);
        }
        else
        {
            for (var row = 0; row < height; row++)
                Marshal.Copy(source + row * rowStride, dst, row * width, width);
        }

        return true;
    }

    private bool RepackChromaToNv12(AImage.Plane uPlane, AImage.Plane vPlane, int width, int height, byte[] dst)
    {
        if (uPlane.Buffer is not { } uBuffer || vPlane.Buffer is not { } vBuffer)
            return false;

        var uSource = JNIEnv.GetDirectBufferAddress(uBuffer.Handle);
        var vSource = JNIEnv.GetDirectBufferAddress(vBuffer.Handle);
        if (uSource == IntPtr.Zero || vSource == IntPtr.Zero)
            return false;

        var chromaWidth = width / 2;
        var chromaHeight = height / 2;
        var dstOffset = width * height;
        var dstRowBytes = width; // chromaWidth UV byte pairs per NV12 chroma row

        if (uPlane.PixelStride == 2 && vPlane.PixelStride == 2)
        {
            // Fast path: with PixelStride == 2 the U plane's buffer view over the
            // underlying semi-planar allocation is already the interleaved UV (NV12)
            // byte sequence. Copy rows directly; the view typically ends one byte
            // before the final V sample, which is filled in from the V plane below.
            var uRowStride = uPlane.RowStride;
            var uAvailable = uBuffer.Remaining();
            for (var row = 0; row < chromaHeight; row++)
            {
                var srcPos = row * uRowStride;
                var count = Math.Min(dstRowBytes, uAvailable - srcPos);
                if (count <= 0)
                    break;
                Marshal.Copy(uSource + srcPos, dst, dstOffset + row * dstRowBytes, count);
            }

            var lastV = (chromaHeight - 1) * vPlane.RowStride + (chromaWidth - 1) * vPlane.PixelStride;
            dst[dstOffset + chromaHeight * dstRowBytes - 1] = Marshal.ReadByte(vSource, lastV);
            return true;
        }

        // General fallback (fully planar or exotic strides): stage each chroma plane
        // into a reusable scratch array with one native copy, then interleave per pixel.
        var uLength = uBuffer.Remaining();
        var vLength = vBuffer.Remaining();
        var scratchU = EnsureScratch(ref _chromaScratchU, uLength);
        var scratchV = EnsureScratch(ref _chromaScratchV, vLength);
        Marshal.Copy(uSource, scratchU, 0, uLength);
        Marshal.Copy(vSource, scratchV, 0, vLength);

        var uPixelStride = uPlane.PixelStride;
        var vPixelStride = vPlane.PixelStride;
        var uRowStrideGeneral = uPlane.RowStride;
        var vRowStrideGeneral = vPlane.RowStride;

        for (var row = 0; row < chromaHeight; row++)
        {
            var uRow = row * uRowStrideGeneral;
            var vRow = row * vRowStrideGeneral;
            var dstRow = dstOffset + row * dstRowBytes;
            for (var col = 0; col < chromaWidth; col++)
            {
                var uIndex = uRow + col * uPixelStride;
                var vIndex = vRow + col * vPixelStride;
                if (uIndex >= uLength || vIndex >= vLength)
                    break;
                dst[dstRow + col * 2] = scratchU[uIndex];       // NV12: U (Cb) first
                dst[dstRow + col * 2 + 1] = scratchV[vIndex];   // then V (Cr)
            }
        }

        return true;
    }

    private static byte[] EnsureScratch(ref byte[]? scratch, int length)
    {
        if (scratch is null || scratch.Length < length)
            scratch = new byte[length];
        return scratch;
    }

    // ---------------------------------------------------------------- shared

    private Handler StartCaptureThread()
    {
        var thread = new HandlerThread("ndi-video-capture");
        thread.Start();
        var looper = thread.Looper
            ?? throw new InvalidOperationException("Capture HandlerThread has no looper after Start().");
        var handler = new Handler(looper);
        _handlerThread = thread;
        return handler;
    }

    /// <summary>Adapts ImageReader availability callbacks to an instance method.</summary>
    private sealed class ImageListener : Java.Lang.Object, ImageReader.IOnImageAvailableListener
    {
        private readonly Action<ImageReader> _onImage;

        public ImageListener(Action<ImageReader> onImage) => _onImage = onImage;

        public void OnImageAvailable(ImageReader? reader)
        {
            if (reader is not null)
                _onImage(reader);
        }
    }

    /// <summary>
    /// Tracks default-display rotation changes for the camera orientation compensation.
    /// DisplayManager fires on every rotation of the display — including one forced with
    /// `settings put system user_rotation` while auto-rotate is off — and depends on neither MAUI,
    /// nor the device-orientation sensor, nor a foreground activity.
    /// </summary>
    private sealed class DisplayRotationListener : Java.Lang.Object, DisplayManager.IDisplayListener
    {
        private readonly AndroidVideoCaptureSource _owner;

        public DisplayRotationListener(AndroidVideoCaptureSource owner) => _owner = owner;

        public void OnDisplayAdded(int displayId) { }

        public void OnDisplayRemoved(int displayId) { }

        public void OnDisplayChanged(int displayId)
        {
            // Native handler-thread callback — never throw into the runtime.
            try { _owner.OnDisplayRotationChanged(displayId); }
            catch { /* ignore — orientation tracking must never kill the capture thread */ }
        }
    }

    /// <summary>
    /// Required on API 34+ before createVirtualDisplay; stops capture cleanly when
    /// the system (or the user, via the status bar) revokes the projection.
    /// </summary>
    private sealed class ProjectionCallback : MediaProjection.Callback
    {
        private readonly AndroidVideoCaptureSource _owner;

        public ProjectionCallback(AndroidVideoCaptureSource owner) => _owner = owner;

        public override void OnStop()
        {
            _owner.RaiseStopped(CaptureStopReason.ProjectionStopped,
                "Screen capture was stopped (system revoked or the user ended it from the share indicator).");
            _owner.StopAsync().FireAndForget();
        }
    }

    private sealed class CameraStateCallback : CameraDevice.StateCallback
    {
        private readonly AndroidVideoCaptureSource _owner;
        private readonly TaskCompletionSource<CameraDevice> _opened;

        public CameraStateCallback(AndroidVideoCaptureSource owner, TaskCompletionSource<CameraDevice> opened)
        {
            _owner = owner;
            _opened = opened;
        }

        public override void OnOpened(CameraDevice camera) => _opened.TrySetResult(camera);

        public override void OnDisconnected(CameraDevice camera) =>
            HandleLoss(camera, CaptureStopReason.CameraDisconnected, new InvalidOperationException("Camera was disconnected."));

        public override void OnError(CameraDevice camera, CameraError error) =>
            HandleLoss(camera, CaptureStopReason.CameraError, new InvalidOperationException($"Camera reported error '{error}'."));

        private void HandleLoss(CameraDevice camera, CaptureStopReason reason, Exception error)
        {
            try
            {
                // During open: fault the awaiter. After open: stop the whole source cleanly.
                if (!_opened.TrySetException(error))
                {
                    _owner.RaiseStopped(reason, error.Message);
                    _owner.StopAsync().FireAndForget();
                }
                camera.Close();
            }
            catch
            {
                // Native camera callbacks must never throw into the runtime.
            }
        }
    }

    private sealed class SessionStateCallback : CameraCaptureSession.StateCallback
    {
        private readonly TaskCompletionSource<CameraCaptureSession> _configured;

        public SessionStateCallback(TaskCompletionSource<CameraCaptureSession> configured) =>
            _configured = configured;

        public override void OnConfigured(CameraCaptureSession session) =>
            _configured.TrySetResult(session);

        public override void OnConfigureFailed(CameraCaptureSession session) =>
            _configured.TrySetException(new InvalidOperationException("Camera capture session configuration failed."));
    }

    /// <summary>
    /// Runs Camera2 session state callbacks on the capture HandlerThread —
    /// SessionConfiguration (API 28+) takes an Executor where the legacy overload took a Handler.
    /// </summary>
    private sealed class HandlerExecutor : Java.Lang.Object, Java.Util.Concurrent.IExecutor
    {
        private readonly Handler _handler;

        public HandlerExecutor(Handler handler) => _handler = handler;

        public void Execute(Java.Lang.IRunnable? command)
        {
            if (command is null)
                return;
            // Post returns false once the looper has quit (teardown race): run inline so an
            // OnConfigureFailed/OnClosed is never silently dropped.
            if (!_handler.Post(command))
                command.Run();
        }
    }
}
