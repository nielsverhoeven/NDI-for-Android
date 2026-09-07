using Microsoft.Maui.Accessibility;
using Microsoft.Maui.ApplicationModel;

namespace NdiForAndroid.Services;

/// <summary>
/// <see cref="IScreenReaderAnnouncer"/> backed by MAUI's <see cref="SemanticScreenReader"/> (on
/// Android: View.AnnounceForAccessibility on the decor view). Marshals to the main thread because
/// ViewModel status writes can originate off it via <see cref="IMainThreadDispatcher"/> (#345).
/// </summary>
public sealed class MauiScreenReaderAnnouncer : IScreenReaderAnnouncer
{
    public void Announce(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        try
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                try
                {
                    SemanticScreenReader.Default.Announce(message);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Screen reader announce failed: {ex.Message}");
                }
            });
        }
        catch (Exception ex)
        {
            // No window yet (announce during startup) — nothing to read to, never crash for it.
            System.Diagnostics.Debug.WriteLine($"Screen reader announce not dispatched: {ex.Message}");
        }
    }
}
