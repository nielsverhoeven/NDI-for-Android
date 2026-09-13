using AndroidX.AppCompat.App;
using Microsoft.Maui.ApplicationModel;
using NdiForAndroid.Services;
using ScreenOrientation = Android.Content.PM.ScreenOrientation;

namespace NdiForAndroid.Platforms.Android.Services;

/// <summary>
/// Locks/unlocks the Android activity's screen orientation for the full-screen viewer. Mirrors
/// <see cref="AndroidImmersiveModeService"/>'s self-marshalling pattern.
/// </summary>
public sealed class AndroidOrientationLockService : IOrientationLockService
{
    public void RequestLandscape() =>
        MainThread.BeginInvokeOnMainThread(() => SetOrientation(ScreenOrientation.SensorLandscape));

    public void RequestPortrait() =>
        MainThread.BeginInvokeOnMainThread(() => SetOrientation(ScreenOrientation.Portrait));

    /// <summary>Unspecified, not FullSensor — FullSensor would override the user's own system
    /// auto-rotate lock, a behavioural regression this app has never had.</summary>
    public void Release() =>
        MainThread.BeginInvokeOnMainThread(() => SetOrientation(ScreenOrientation.Unspecified));

    private static void SetOrientation(ScreenOrientation orientation)
    {
        if (Platform.CurrentActivity is AppCompatActivity activity)
            activity.RequestedOrientation = orientation;
    }
}
