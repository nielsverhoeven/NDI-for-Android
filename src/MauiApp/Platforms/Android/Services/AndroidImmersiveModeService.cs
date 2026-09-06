using AndroidX.AppCompat.App;
using AndroidX.Core.View;
using AndroidX.Fragment.App;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using NdiForAndroid.Services;
using PlatformWindow = Android.Views.Window;

namespace NdiForAndroid.Platforms.Android.Services;

/// <summary>
/// Hides/shows Android system bars and toggles keep-screen-on for the full-screen viewer.
/// Mirrors the API-30 guard used by <c>MauiAppearanceService.UpdateAndroidStatusBar</c>.
/// The full-screen page is presented in a modal <see cref="DialogFragment"/> with its own
/// window, so the activity window and that dialog's window are both targeted.
/// </summary>
public sealed class AndroidImmersiveModeService : IImmersiveModeService
{
    public void EnterImmersive()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            foreach (var window in GetTargetWindows())
            {
                WindowCompat.SetDecorFitsSystemWindows(window, false);
                var controller = WindowCompat.GetInsetsController(window, window.DecorView);
                if (controller is null)
                    continue;

                controller.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
                controller.Hide(WindowInsetsCompat.Type.SystemBars());
            }
        });
    }

    public void ExitImmersive()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            foreach (var window in GetTargetWindows())
                WindowCompat.GetInsetsController(window, window.DecorView)?.Show(WindowInsetsCompat.Type.SystemBars());
        });
    }

    public void KeepScreenOn(bool enabled)
    {
        MainThread.BeginInvokeOnMainThread(() => DeviceDisplay.Current.KeepScreenOn = enabled);
    }

    private static IEnumerable<PlatformWindow> GetTargetWindows()
    {
        if (!OperatingSystem.IsAndroidVersionAtLeast(30))
            yield break;

        if (Platform.CurrentActivity is not AppCompatActivity activity)
            yield break;

        if (activity.Window is { } activityWindow)
            yield return activityWindow;

        if (FindTopModalDialogWindow(activity.SupportFragmentManager) is { } dialogWindow)
            yield return dialogWindow;
    }

    private static PlatformWindow? FindTopModalDialogWindow(FragmentManager? fragmentManager)
    {
        if (fragmentManager is null)
            return null;

        PlatformWindow? found = null;
        foreach (var fragment in fragmentManager.Fragments)
        {
            if (fragment is DialogFragment { Dialog.Window: { } window })
                found = window; // last match wins — the top-most modal in z-order

            if (fragment.IsAdded && FindTopModalDialogWindow(fragment.ChildFragmentManager) is { } nested)
                found = nested;
        }

        return found;
    }
}
