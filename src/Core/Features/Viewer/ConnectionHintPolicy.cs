using NdiForAndroid.NdiBridge;
using NdiForAndroid.Services;

namespace NdiForAndroid.Features.Viewer;

/// <summary>
/// Decides when the viewer shows a "connection weak" hint from the bridge's once-per-second
/// fps / dropped-frame statistics. It never changes the profile — the hint only suggests and
/// the user picks (#331 decision 2026-09-04: no automatic degradation).
/// </summary>
public static class ConnectionHintPolicy
{
    public const float WeakFpsThreshold = 15f;
    public const float WeakDropPercentThreshold = 30f;
    public const float GoodFpsThreshold = 15f;
    public const float GoodDropPercentThreshold = 10f;

    /// <summary>Consecutive weak 1 s samples before the hint appears (sample = one bridge stats tick).</summary>
    public const int WeakSamplesToShow = 5;

    /// <summary>Consecutive good 1 s samples before an active hint clears.</summary>
    public const int GoodSamplesToClear = 5;

    public const string WeakHint = "Connection weak";
    public const string WeakHintTrySmooth = "Connection weak — try Smooth";

    /// <summary>RSSI at or below which a Wi-Fi link is a plausible cause of a weak stream. -70 dBm
    /// is the conventional "poor" boundary.</summary>
    public const int WeakRssiDbm = -70;

    /// <summary>PHY link speed (Mbit/s) at or below which a full-bandwidth 1080p NDI stream cannot
    /// fit whatever the RSSI says — full NDI at 1080p is ~100+ Mbit/s and the usable share of a PHY
    /// rate is well under half.</summary>
    public const int WeakLinkSpeedMbps = 50;

    /// <summary>
    /// True when the radio link is itself a plausible cause, so the hint can name it instead of
    /// blaming the stream in the abstract. Requires an actual Wi-Fi association: an unknown link
    /// (non-Android host, wired, failed read) never produces a radio claim.
    /// </summary>
    public static bool IsWeakLink(NetworkLinkSnapshot link) =>
        link.IsWifi
        && (link.Band == WifiBand.TwoPointFourGhz
            || (link.Rssi != NetworkLinkSnapshot.UnknownRssi && link.Rssi <= WeakRssiDbm)
            || (link.LinkSpeedMbps != NetworkLinkSnapshot.UnknownLinkSpeed
                && link.LinkSpeedMbps <= WeakLinkSpeedMbps));

    /// <summary>Band name for user-facing copy. Never invents a band the device is not on.</summary>
    public static string LinkDescription(NetworkLinkSnapshot link) => link.Band switch
    {
        WifiBand.TwoPointFourGhz => "2.4 GHz",
        WifiBand.FiveGhz => "5 GHz",
        WifiBand.SixGhz => "6 GHz",
        _ => "Wi-Fi",
    };

    /// <summary>
    /// Hint text for the active profile and the link it is running over, or null when no hint should
    /// show. The link only ever changes the wording — this class never calls SetQualityProfile and
    /// nothing downstream of it does either.
    /// </summary>
    public static string? HintText(bool isHintActive, QualityProfile profile, NetworkLinkSnapshot link)
    {
        if (!isHintActive)
            return null;

        if (!IsWeakLink(link))
            return HintText(isHintActive, profile);

        var prefix = $"{LinkDescription(link)} / weak signal";
        return profile == QualityProfile.Smooth ? prefix : prefix + " — switch to Smooth";
    }

    public static bool IsWeak(float fps, float dropPercent) =>
        fps < WeakFpsThreshold || dropPercent > WeakDropPercentThreshold;

    public static bool IsGood(float fps, float dropPercent) =>
        fps > GoodFpsThreshold && dropPercent < GoodDropPercentThreshold;

    /// <summary>
    /// Feeds one sample and returns the new state. Hysteresis: a sample that is neither weak nor
    /// good resets both runs without changing whether the hint is active.
    /// </summary>
    public static State Next(State state, float fps, float dropPercent)
    {
        if (IsWeak(fps, dropPercent))
        {
            var weak = state.WeakRun + 1;
            return new State(weak, 0, state.IsHintActive || weak >= WeakSamplesToShow);
        }

        if (IsGood(fps, dropPercent))
        {
            var good = state.GoodRun + 1;
            return new State(0, good, state.IsHintActive && good < GoodSamplesToClear);
        }

        return new State(0, 0, state.IsHintActive);
    }

    /// <summary>Hint text for the active profile, or null when no hint should show.</summary>
    public static string? HintText(bool isHintActive, QualityProfile profile) => !isHintActive
        ? null
        : profile == QualityProfile.Smooth ? WeakHint : WeakHintTrySmooth;

    public readonly record struct State(int WeakRun, int GoodRun, bool IsHintActive)
    {
        public static readonly State Idle = new(0, 0, false);
    }
}
