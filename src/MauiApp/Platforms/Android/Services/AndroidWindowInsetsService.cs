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
        var root = ViewCompat.GetRootWindowInsets(decorView);
        var insets = root?.GetInsets(WindowInsetsCompat.Type.NavigationBars());

        if (insets is null)
        {
            // Logged rather than silently returning zero. The first attempt at this fix changed
            // nothing on screen — the rail stayed at x=28, which is its 8dp margin alone — and a
            // zero inset and a never-called accessor look identical from outside the app. The tag
            // is package-prefixed so the CI logcat filter keeps it.
            Log("navigation-bar insets unavailable: " +
                (root is null ? "GetRootWindowInsets returned null" : "GetInsets returned null"));
            return EdgeInsets.Zero;
        }

        var result = new EdgeInsets(
            insets.Left   / density,
            insets.Top    / density,
            insets.Right  / density,
            insets.Bottom / density);

        Log($"navigation-bar insets px L={insets.Left} T={insets.Top} R={insets.Right} " +
            $"B={insets.Bottom}, density={density}, dp L={result.Left:0.#} T={result.Top:0.#} " +
            $"R={result.Right:0.#} B={result.Bottom:0.#}");

        return result;
    }

    /// <summary>
    /// Diagnostic trace for the inset reads, tagged so the CI logcat filter keeps it.
    /// </summary>
    /// <remarks>
    /// The tag is deliberately package-prefixed: the emulator run script filters logcat on
    /// <c>com.ndi.android</c> among other patterns, so a tag that does not contain it is captured
    /// and then thrown away before anyone reads the log.
    /// </remarks>
    private static void Log(string message) =>
        global::Android.Util.Log.Info("com.ndi.android.insets", message);

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
