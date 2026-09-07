using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using NdiForAndroid.NdiBridge;
using NdiForAndroid.Services;

namespace NdiForAndroid.Platforms.Android.Services;

[Service(
    Exported = false,
    Name = "com.ndi.android.ScreenShareForegroundService",
    ForegroundServiceType = ForegroundService.TypeMediaProjection | ForegroundService.TypeCamera | ForegroundService.TypeMicrophone)]
public sealed class ScreenShareForegroundService : Service
{
    internal const string ActionStart = "com.ndi.android.action.START_SCREEN_SHARE";
    internal const string ActionStop = "com.ndi.android.action.STOP_SCREEN_SHARE";
    internal const string ActionStopRequested = "com.ndi.android.action.STOP_REQUESTED";
    internal const string ExtraStreamName = "extra_stream_name";
    internal const string ExtraCaptureKind = "extra_capture_kind";

    private const string ChannelId = "ndi_screen_share";
    private const int NotificationId = 4107;
    private const int StopActionRequestCode = 4108;

    private INdiOutputBridge? _bridge;
    private string? _streamName;

    public override IBinder? OnBind(Intent? intent) => null;

    /// <summary>
    /// Belt-and-braces unsubscribe: the system can destroy this service outright (low memory,
    /// process death) without ever delivering <see cref="ActionStop"/>/<see cref="ActionStopRequested"/>,
    /// which would otherwise leave the bridge holding a handler into a dead service instance.
    /// </summary>
    public override void OnDestroy()
    {
        UnsubscribeFromBridge();
        base.OnDestroy();
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent is null)
        {
            // Sticky restart with no live capture session to resume; starting foreground
            // with TypeMediaProjection here throws SecurityException on API 34+.
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        if (intent.Action == ActionStopRequested)
        {
            // Teardown flows through the bridge; it re-enters via ActionStop below once
            // IScreenSharePlatformService.StopForegroundSessionAsync() sends that intent.
            var bridge = IPlatformApplication.Current?.Services.GetService<INdiOutputBridge>();
            if (bridge is null || !bridge.IsActive)
            {
                StopForeground(StopForegroundFlags.Remove);
                UnsubscribeFromBridge();
                StopSelf();
            }
            else
            {
                bridge.StopOutputAsync().FireAndForget();
            }

            return StartCommandResult.NotSticky;
        }

        if (intent.Action == ActionStop)
        {
            StopForeground(StopForegroundFlags.Remove);
            UnsubscribeFromBridge();
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        _streamName = intent.GetStringExtra(ExtraStreamName);
        var kindValue = intent.GetIntExtra(ExtraCaptureKind, (int)VideoInputKind.Screen);
        var kind = (VideoInputKind)kindValue;
        var notification = BuildNotification(_streamName);

        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            // API 29+: declare the active service types explicitly. Passing a type
            // whose runtime permission is not granted throws SecurityException on
            // API 34+, so camera/microphone are only included when currently granted.
            StartForeground(NotificationId, notification, GetGrantedServiceTypes(kind));
        }
        else
        {
            StartForeground(NotificationId, notification);
        }

        // #370 home-nav-07: keep the notification text current with the live connection count.
        SubscribeToBridge();

        return StartCommandResult.Sticky;
    }

    /// <summary>
    /// Refreshes the notification text with the live connection count whenever the bridge
    /// reports a status change (#370 home-nav-07). Runs on whatever thread the bridge raises the
    /// event on — <c>NotificationManager.Notify</c> is thread-safe — and never lets a failure
    /// here take the pump thread down.
    /// </summary>
    private void SubscribeToBridge()
    {
        if (_bridge is not null)
            return;

        _bridge = IPlatformApplication.Current?.Services.GetService<INdiOutputBridge>();
        if (_bridge is not null)
            _bridge.OutputStatusChanged += OnBridgeStatusChanged;
    }

    private void UnsubscribeFromBridge()
    {
        if (_bridge is null)
            return;

        _bridge.OutputStatusChanged -= OnBridgeStatusChanged;
        _bridge = null;
    }

    private void OnBridgeStatusChanged(object? sender, EventArgs e)
    {
        try
        {
            var manager = (NotificationManager?)GetSystemService(NotificationService);
            manager?.Notify(NotificationId, BuildNotification(_streamName));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Foreground notification refresh failed: {ex}");
        }
    }

    [System.Runtime.Versioning.SupportedOSPlatform("android29.0")]
    private ForegroundService GetGrantedServiceTypes(VideoInputKind kind)
    {
        ForegroundService types = 0;

        // MediaProjection is gated by the consent dialog (not a runtime permission)
        // and the caller only starts this service after consent, so it is always safe
        // — but only for a screen-capture session.
        if (kind == VideoInputKind.Screen)
            types |= ForegroundService.TypeMediaProjection;

        // Camera/microphone service types exist from API 30.
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            if (kind is VideoInputKind.CameraFront or VideoInputKind.CameraRear
                && CheckSelfPermission(global::Android.Manifest.Permission.Camera) == Permission.Granted)
                types |= ForegroundService.TypeCamera;

            if (CheckSelfPermission(global::Android.Manifest.Permission.RecordAudio) == Permission.Granted)
                types |= ForegroundService.TypeMicrophone;
        }

        return types;
    }

    private Notification BuildNotification(string? streamName)
    {
        var manager = (NotificationManager?)GetSystemService(NotificationService);
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var channel = new NotificationChannel(
                ChannelId,
                "NDI Screen Share",
                NotificationImportance.Low)
            {
                Description = "Foreground service for NDI screen share output",
            };
            manager?.CreateNotificationChannel(channel);
        }

        // The AndroidX bindings annotate the fluent Set* returns as nullable even though
        // they always return 'this'; call them statement-style to avoid null-chaining.
        var builder = new NotificationCompat.Builder(this, ChannelId);
        builder.SetContentTitle("NDI Output Active");

        // #370 home-nav-07: live connection count, so the notification stays a genuine status
        // readout rather than a static "it's running" text.
        var connections = _bridge?.ConnectionCount ?? 0;
        var connectionsText = connections == 1 ? "1 connection" : $"{connections} connections";
        builder.SetContentText($"Streaming: {streamName ?? "NDI-Android"} — {connectionsText}");

        // #370 home-nav-06: a neutral monochrome glyph, not the OS warning triangle — this
        // notification communicates "output is running", not an error condition.
        builder.SetSmallIcon(Resource.Drawable.ic_stat_ndi);
        builder.SetOngoing(true);

        var stopIntent = new Intent(this, typeof(ScreenShareForegroundService));
        stopIntent.SetAction(ActionStopRequested);
        var stopPendingIntent = PendingIntent.GetService(
            this, StopActionRequestCode, stopIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable);
        builder.AddAction(new NotificationCompat.Action.Builder(
            global::Android.Resource.Drawable.IcMenuCloseClearCancel, "Stop", stopPendingIntent).Build());

        // NotificationCompat.Builder.Build() is non-null in practice for a well-formed builder.
        return builder.Build()!;
    }
}
