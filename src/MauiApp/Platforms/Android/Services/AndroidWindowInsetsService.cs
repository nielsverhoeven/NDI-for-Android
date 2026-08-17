using AndroidX.Core.View;
using NdiForAndroid.Services;

namespace NdiForAndroid.Platforms.Android.Services;

/// <summary>
/// Reads the real status-bar inset from the current window.
/// </summary>
public sealed class AndroidWindowInsetsService : IWindowInsetsService
{
    private InsetsListener? _listener;

    public event EventHandler? InsetsChanged;

    public double GetStatusBarInset()
    {
        var activity = Platform.CurrentActivity;
        var decorView = activity?.Window?.DecorView;
        if (decorView is null)
            return 0d;

        EnsureObserving(decorView);

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

        EnsureObserving(decorView);

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
    /// Kept, but do not rely on it: no line from here has ever reached the CI logcat, even with
    /// the buffer enlarged and captured continuously from before install, while native
    /// <c>monodroid-assembly</c> lines from the same process arrive normally. The reason is not
    /// understood. The rendered geometry turned out to be the better instrument anyway — the rail
    /// item's position states the applied padding exactly.
    /// </remarks>
    private static void Log(string message) =>
        global::Android.Util.Log.Info("com.ndi.android.insets", message);

    /// <summary>
    /// Subscribes to the window's inset dispatch, once per decor view.
    /// </summary>
    /// <remarks>
    /// Registered here rather than in <c>MainActivity</c> so that the object which owns reading
    /// insets also owns knowing when they change, and so a caller cannot subscribe to
    /// <see cref="InsetsChanged"/> and silently never be told.
    /// </remarks>
    private void EnsureObserving(global::Android.Views.View decorView)
    {
        if (_listener is not null)
            return;

        _listener = new InsetsListener(() => InsetsChanged?.Invoke(this, EventArgs.Empty));
        ViewCompat.SetOnApplyWindowInsetsListener(decorView, _listener);
    }

    private sealed class InsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
    {
        private readonly Action _onChanged;

        public InsetsListener(Action onChanged) => _onChanged = onChanged;

        public WindowInsetsCompat OnApplyWindowInsets(global::Android.Views.View v, WindowInsetsCompat insets)
        {
            _onChanged();

            // Returned unconsumed. The window draws edge-to-edge and other views — Shell's own
            // chrome included — still need to see these; swallowing them here would fix the rail
            // by breaking everything below it.
            return insets;
        }
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
