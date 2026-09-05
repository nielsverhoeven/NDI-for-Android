namespace NdiForAndroid.Services;

/// <summary>
/// Non-Android fallback: no system bars, so nothing to inset.
/// </summary>
public sealed class NoopWindowInsetsService : IWindowInsetsService
{
    public double GetStatusBarInset() => 0d;
}
