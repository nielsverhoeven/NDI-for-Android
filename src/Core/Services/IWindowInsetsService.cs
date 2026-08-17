namespace NdiForAndroid.Services;

/// <summary>
/// Insets in device-independent units, per edge.
/// </summary>
public readonly record struct EdgeInsets(double Left, double Top, double Right, double Bottom)
{
    public static EdgeInsets Zero => new(0, 0, 0, 0);
}

/// <summary>
/// Exposes the system-bar insets of the current window in device-independent units.
/// </summary>
/// <remarks>
/// The app draws edge-to-edge (<c>SetDecorFitsSystemWindows(false)</c> in <c>MainActivity</c>),
/// so chrome anchored to an edge of the window renders <em>behind</em> the system bars unless it
/// is inset by hand. Non-Android targets have no system bars and report zero.
/// </remarks>
public interface IWindowInsetsService
{
    /// <summary>
    /// Height of the status bar in device-independent units, or <c>0</c> when there is none
    /// (or it cannot be resolved yet). Read it late — the value is only reliable once the
    /// window has been laid out.
    /// </summary>
    double GetStatusBarInset();

    /// <summary>
    /// Navigation-bar insets per edge, in device-independent units.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Per edge, not a single height, because the navigation bar does not stay on the bottom. In
    /// landscape it moves to whichever side the rotation puts it on — and on a left rotation that
    /// is the same edge the navigation rail occupies.
    /// </para>
    /// <para>
    /// That is not cosmetic. Measured on the CI emulator in landscape, the bar owns x 0..168 while
    /// a rail item spans x 28..224: three quarters of the item, and its centre, are underneath
    /// system UI. A press there is delivered to the system rather than to the app, so tapping a
    /// rail item went Home instead of navigating (#321).
    /// </para>
    /// </remarks>
    EdgeInsets GetNavigationBarInsets();
}
