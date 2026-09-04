using AndroidX.Core.View;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using NdiForAndroid.Services;

namespace NdiForAndroid.Platforms.Android.Services;

/// <summary>
/// Hides/shows Android system bars and toggles keep-screen-on for the full-screen viewer.
/// Mirrors the API-30 guard used by <c>MauiAppearanceService.UpdateAndroidStatusBar</c>.
/// </summary>
public sealed class AndroidImmersiveModeService : IImmersiveModeService
{
    public void EnterImmersive()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var controller = GetInsetsController();
            if (controller is null)
                return;

            controller.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
            controller.Hide(WindowInsetsCompat.Type.SystemBars());
        });
    }

    public void ExitImmersive()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var controller = GetInsetsController();
            controller?.Show(WindowInsetsCompat.Type.SystemBars());
        });
    }

    public void KeepScreenOn(bool enabled)
    {
        MainThread.BeginInvokeOnMainThread(() => DeviceDisplay.Current.KeepScreenOn = enabled);
    }

    private static WindowInsetsControllerCompat? GetInsetsController()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(30))
            return null;

        var activity = Platform.CurrentActivity;
        if (activity?.Window is null)
            return null;

        return WindowCompat.GetInsetsController(activity.Window, activity.Window.DecorView);
    }
}
