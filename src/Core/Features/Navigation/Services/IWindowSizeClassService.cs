namespace NdiForAndroid.Features.Navigation.Services;

/// <summary>
/// Material Design window width size classes.
/// Compact &lt; 600dp, Medium 600–840dp, Expanded &gt; 840dp.
/// </summary>
public enum WindowSizeClass
{
    Compact,
    Medium,
    Expanded,
}

/// <summary>
/// Tracks the current window width size class. Fed from the shell's
/// <c>OnSizeAllocated</c> (device-independent units), so <see cref="Changed"/>
/// is always raised on the UI thread.
/// </summary>
public interface IWindowSizeClassService
{
    WindowSizeClass Current { get; }

    /// <summary>Raised on the UI thread, only when the size class transitions.</summary>
    event EventHandler<WindowSizeClass>? Changed;

    /// <summary>Idempotent: raises <see cref="Changed"/> only on class transitions.</summary>
    void UpdateFromWidth(double widthDp);
}
