using AndroidX.Core.View;
using NdiForAndroid.Services;

namespace NdiForAndroid.Platforms.Android.Services;

/// <summary>
/// Reads the real status-bar inset from the current window.
/// </summary>
public sealed class AndroidWindowInsetsService : IWindowInsetsService
{
    public double GetStatusBarInset()
    {
        var activity = Platform.CurrentActivity;
        var decorView = activity?.Window?.DecorView;
        if (decorView is null)
            return 0d;

        var density = activity!.Resources?.DisplayMetrics?.Density ?? 0f;
        if (density <= 0f)
            return 0d;

        var insetPixels = ResolveStatusBarInsetPixels(decorView, activity);
        return insetPixels <= 0 ? 0d : insetPixels / density;
    }

    public EdgeInsets GetNavigationBarInsets()
    {
        var activity = Platform.CurrentActivity;
        var decorView = activity?.Window?.DecorView;
        if (decorView is null)
            return EdgeInsets.Zero;

        var density = activity!.Resources?.DisplayMetrics?.Density ?? 0f;
        if (density <= 0f)
            return EdgeInsets.Zero;

        // No dimen fallback here, unlike the status bar. navigation_bar_height is a single value
        // that says nothing about which edge the bar is on, so guessing from it would be worse
        // than reporting zero: it would inset the wrong side and move the rail away from the bar
        // rather than out from under it.
        var insets = ViewCompat.GetRootWindowInsets(decorView)
            ?.GetInsets(WindowInsetsCompat.Type.NavigationBars());

        if (insets is null)
            return EdgeInsets.Zero;

        return new EdgeInsets(
            insets.Left   / density,
            insets.Top    / density,
            insets.Right  / density,
            insets.Bottom / density);
    }

    private static int ResolveStatusBarInsetPixels(
        global::Android.Views.View decorView,
        global::Android.App.Activity activity)
    {
        // Preferred source: the live insets attached to the view tree. Null until the window
        // has been attached and laid out, which is why the dimen fallback below exists.
        var rootInsets = ViewCompat.GetRootWindowInsets(decorView);
        var statusBarInset = rootInsets?.GetInsets(WindowInsetsCompat.Type.StatusBars())?.Top ?? 0;
        if (statusBarInset > 0)
            return statusBarInset;

        // Fallback: the platform's declared status-bar height. Always resolvable, but it does
        // not account for display cutouts, so it is only used before the first layout pass.
        var resources = activity.Resources;
        if (resources is null)
            return 0;

        var resourceId = resources.GetIdentifier("status_bar_height", "dimen", "android");
        return resourceId > 0 ? resources.GetDimensionPixelSize(resourceId) : 0;
    }
}
