namespace NdiForAndroid.Services;

/// <summary>
/// Non-Android fallback: no system bars, so nothing to inset.
/// </summary>
public sealed class NoopWindowInsetsService : IWindowInsetsService
{
    public double GetStatusBarInset() => 0d;

    public EdgeInsets GetNavigationBarInsets() => EdgeInsets.Zero;

    /// <summary>Never raised: with no system bars there is nothing to change.</summary>
    public event EventHandler? InsetsChanged
    {
        add { }
        remove { }
    }
}
