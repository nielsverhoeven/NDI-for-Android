using Microsoft.Maui.Accessibility;

namespace NdiForAndroid.Services;

/// <summary>
/// <see cref="IAccessibilityAnnouncer"/> over MAUI's <see cref="SemanticScreenReader"/>. On Android
/// this raises a TYPE_ANNOUNCEMENT accessibility event, which TalkBack speaks; when no assistive
/// technology is enabled the platform drops it.
/// </summary>
public sealed class MauiAccessibilityAnnouncer : IAccessibilityAnnouncer
{
    public void Announce(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        try
        {
            SemanticScreenReader.Default.Announce(message);
        }
        catch (Exception)
        {
            // Best-effort only: an announcement that cannot be delivered must never surface as a crash.
        }
    }
}
