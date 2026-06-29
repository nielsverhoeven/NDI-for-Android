namespace NdiForAndroid.Features.AppState.Models;

/// <summary>
/// Immutable snapshot of all persisted application state that survives backgrounding and process death.
/// </summary>
public sealed record AppStateSnapshot
{
    public string? LastViewerSourceId { get; }
    public string? StreamName { get; }
    public bool IsOutputActive { get; }
    public string? LastSelectedSourceId { get; }

    public AppStateSnapshot(
        string? lastViewerSourceId,
        string? streamName,
        bool isOutputActive,
        string? lastSelectedSourceId)
    {
        LastViewerSourceId = lastViewerSourceId;
        StreamName = streamName;
        IsOutputActive = isOutputActive;
        LastSelectedSourceId = lastSelectedSourceId;
    }

    public static AppStateSnapshot Empty => new(null, null, false, null);
}
