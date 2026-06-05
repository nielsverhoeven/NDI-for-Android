using Android.Content;
using Android.OS;
using NdiForAndroid.Services;

namespace NdiForAndroid.Platforms.Android.Services;

public sealed class AndroidScreenSharePlatformService : IScreenSharePlatformService
{
    private readonly Context _context;

    public AndroidScreenSharePlatformService()
    {
        _context = global::Android.App.Application.Context;
    }

    public bool IsForegroundServiceActive { get; private set; }

    public Task StartForegroundSessionAsync(string streamName, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var intent = new Intent(_context, typeof(ScreenShareForegroundService));
        intent.SetAction(ScreenShareForegroundService.ActionStart);
        intent.PutExtra(ScreenShareForegroundService.ExtraStreamName, streamName);

        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
            _context.StartForegroundService(intent);
        else
            _context.StartService(intent);

        IsForegroundServiceActive = true;
        return Task.CompletedTask;
    }

    public Task StopForegroundSessionAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var intent = new Intent(_context, typeof(ScreenShareForegroundService));
        intent.SetAction(ScreenShareForegroundService.ActionStop);
        _context.StartService(intent);

        IsForegroundServiceActive = false;
        return Task.CompletedTask;
    }
}
