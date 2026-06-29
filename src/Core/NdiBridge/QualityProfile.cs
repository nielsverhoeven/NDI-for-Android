namespace NdiForAndroid.NdiBridge;

/// <summary>Video quality profile for an NDI viewer connection.</summary>
public enum QualityProfile
{
    /// <summary>Smooth — lowest quality, prioritizes frame rate and latency.</summary>
    Smooth,

    /// <summary>Balanced — medium quality (default).</summary>
    Balanced,

    /// <summary>High — highest quality, prioritizes visual fidelity over frame rate.</summary>
    High,
}
