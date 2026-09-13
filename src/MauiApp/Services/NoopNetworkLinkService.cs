namespace NdiForAndroid.Services;

/// <summary>
/// Non-Android twin of <see cref="INetworkLinkService"/>, so Core builds and tests without a
/// device. "Unknown" is the value the hint policy treats as "say nothing about the radio".
/// </summary>
public sealed class NoopNetworkLinkService : INetworkLinkService
{
    public NetworkLinkSnapshot GetSnapshot() => NetworkLinkSnapshot.Unknown;
}
