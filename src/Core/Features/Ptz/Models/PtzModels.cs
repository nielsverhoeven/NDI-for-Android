namespace NdiForAndroid.Features.Ptz.Models;

/// <summary>VISCA-over-TCP endpoint. VISCA device address is fixed at 1.</summary>
public sealed record PtzEndpoint(string Host, int Port)
{
    /// <summary>Well-known raw-VISCA-over-TCP port used by PTZOptics/Avonic-style cameras.</summary>
    public const int DefaultPort = 5678;
}

/// <summary>Connection state of a PTZ backend (NDI-native or VISCA-over-TCP).</summary>
public enum PtzLinkState
{
    Disconnected,
    Connecting,
    Connected,
    Error,
}
