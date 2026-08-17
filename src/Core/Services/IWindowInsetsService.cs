namespace NdiForAndroid.Services;

/// <summary>
/// Exposes the system-bar insets of the current window in device-independent units.
/// </summary>
/// <remarks>
/// The app draws edge-to-edge (<c>SetDecorFitsSystemWindows(false)</c> in <c>MainActivity</c>),
/// so chrome anchored to the top of the window renders <em>behind</em> the status bar unless it
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
}
