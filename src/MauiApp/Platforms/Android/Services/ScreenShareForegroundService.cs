using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using AndroidX.Core.App;

namespace NdiForAndroid.Platforms.Android.Services;

[Service(
    Exported = false,
    Name = "com.ndi.android.ScreenShareForegroundService",
    ForegroundServiceType = ForegroundService.TypeMediaProjection | ForegroundService.TypeCamera | ForegroundService.TypeMicrophone)]
public sealed class ScreenShareForegroundService : Service
{
    internal const string ActionStart = "com.ndi.android.action.START_SCREEN_SHARE";
    internal const string ActionStop = "com.ndi.android.action.STOP_SCREEN_SHARE";
    internal const string ExtraStreamName = "extra_stream_name";

    private const string ChannelId = "ndi_screen_share";
    private const int NotificationId = 4107;

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        if (intent?.Action == ActionStop)
        {
            StopForeground(StopForegroundFlags.Remove);
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        var streamName = intent?.GetStringExtra(ExtraStreamName);
        var notification = BuildNotification(streamName);

        if (OperatingSystem.IsAndroidVersionAtLeast(29))
        {
            // API 29+: declare the active service types explicitly. Passing a type
            // whose runtime permission is not granted throws SecurityException on
            // API 34+, so camera/microphone are only included when currently granted.
            StartForeground(NotificationId, notification, GetGrantedServiceTypes());
        }
        else
        {
            StartForeground(NotificationId, notification);
        }

        return StartCommandResult.Sticky;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("android29.0")]
    private ForegroundService GetGrantedServiceTypes()
    {
        // MediaProjection is gated by the consent dialog (not a runtime permission)
        // and the caller only starts this service after consent, so it is always safe.
        var types = ForegroundService.TypeMediaProjection;

        // Camera/microphone service types exist from API 30.
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
        {
            if (CheckSelfPermission(global::Android.Manifest.Permission.Camera) == Permission.Granted)
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
        builder.SetContentText($"Streaming: {streamName ?? "NDI-Android"}");
        builder.SetSmallIcon(global::Android.Resource.Drawable.StatSysWarning);
        builder.SetOngoing(true);

        // NotificationCompat.Builder.Build() is non-null in practice for a well-formed builder.
        return builder.Build()!;
    }
}
