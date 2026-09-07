namespace NdiForAndroid.Services;

/// <summary>
/// Speaks a short message through the platform screen reader (TalkBack on Android) when one is
/// running; a no-op otherwise. Callers must already be on the UI thread (marshal through
/// <see cref="IMainThreadDispatcher"/> first). Unit tests substitute a mock.
/// </summary>
public interface IAccessibilityAnnouncer
{
    /// <summary>Announces <paramref name="message"/> once. Best-effort: never throws.</summary>
    void Announce(string message);
}
