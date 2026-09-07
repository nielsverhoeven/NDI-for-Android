namespace NdiForAndroid.Services;

/// <summary>
/// Proactively speaks a message through the platform screen reader (TalkBack) without moving
/// focus — WCAG 2.2 SC 4.1.3 Status Messages. ViewModels stay MAUI-free by depending on this
/// rather than on Microsoft.Maui.Accessibility.SemanticScreenReader directly (#345).
/// </summary>
public interface IScreenReaderAnnouncer
{
    /// <summary>Announces <paramref name="message"/>. A no-op when no screen reader is active.</summary>
    void Announce(string message);
}

/// <summary>Records every announcement, in order, for unit tests.</summary>
public sealed class FakeScreenReaderAnnouncer : IScreenReaderAnnouncer
{
    public List<string> Announcements { get; } = [];

    public void Announce(string message) => Announcements.Add(message);
}
