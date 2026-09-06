namespace NdiForAndroid.UITests.Infrastructure;

/// <summary>
/// The suite's one timeout policy.
/// </summary>
/// <remarks>
/// <para>
/// Timeouts used to be written per call site — 10, 12, 15, 20 and 30 seconds appeared across the
/// same file with no stated reason for any of them. Two things went wrong with that. A budget
/// picked too low read as a product failure (a 10s wait for the Settings page failed while its
/// sibling passed at 15s on the same screen), and there was no single place to widen everything
/// when the CI emulator got slower.
/// </para>
/// <para>
/// Four named budgets, chosen by <i>what is being waited for</i> rather than by how flaky a
/// given line felt on the day:
/// </para>
/// </remarks>
public static class Timeouts
{
    /// <summary>
    /// An element that is already on screen and only has to be found. No animation, no I/O.
    /// </summary>
    public static readonly TimeSpan Element = TimeSpan.FromSeconds(10);

    /// <summary>
    /// A screen transition: tap a nav item, wait for the destination to render. Covers Shell's
    /// page swap plus the first layout pass of the incoming page.
    /// </summary>
    public static readonly TimeSpan Navigation = TimeSpan.FromSeconds(20);

    /// <summary>
    /// The app becoming usable from cold. MAUI start-up on a software-rendered emulator runs
    /// well past Appium's 20s default; this repo's own floor for a cold emulator is 30s.
    /// </summary>
    public static readonly TimeSpan AppStart = TimeSpan.FromSeconds(45);

    /// <summary>
    /// Work that crosses the network or the NDI stack — discovery, connect, first frame.
    /// </summary>
    public static readonly TimeSpan Network = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Settle time after an orientation change. Not a timeout — a fixed pause, because there is
    /// no event to wait on: the tree is rebuilt asynchronously and querying it too early
    /// returns the pre-rotation layout, which then fails a position assertion for the wrong
    /// reason.
    /// </summary>
    public static readonly TimeSpan OrientationSettle = TimeSpan.FromMilliseconds(1200);

    /// <summary>
    /// A control's own state reflecting a tap that already landed — a VSM transition or an
    /// auto-save round trip, not a page load.
    /// </summary>
    public static readonly TimeSpan StateChange = TimeSpan.FromSeconds(2);
}
