namespace NdiForAndroid.Features.Navigation.Services;

/// <summary>
/// Pure-logic implementation of <see cref="IWindowSizeClassService"/> (unit-testable, no MAUI deps).
/// Material breakpoints: Compact &lt; 600dp, Medium 600–840dp (inclusive), Expanded &gt; 840dp.
/// </summary>
public sealed class WindowSizeClassService : IWindowSizeClassService
{
    private const double MediumMinWidthDp = 600d;
    private const double MediumMaxWidthDp = 840d;

    public WindowSizeClass Current { get; private set; } = WindowSizeClass.Compact;

    public event EventHandler<WindowSizeClass>? Changed;

    public void UpdateFromWidth(double widthDp)
    {
        var next = Classify(widthDp);
        if (next == Current)
            return;

        Current = next;
        Changed?.Invoke(this, next);
    }

    /// <summary>Shared with callers that classify a width without a live service instance
    /// (e.g. a transient page's own OnSizeAllocated).</summary>
    public static WindowSizeClass Classify(double widthDp) => widthDp switch
    {
        < MediumMinWidthDp => WindowSizeClass.Compact,
        <= MediumMaxWidthDp => WindowSizeClass.Medium,
        _ => WindowSizeClass.Expanded,
    };
}
