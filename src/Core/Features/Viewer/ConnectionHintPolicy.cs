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

    /// <summary>Copy for a weak stream on a 2.4 GHz radio that nevertheless measures fine: the band
    /// is stated as a fact, not asserted as the cause.</summary>
    public const string WeakHintOn24Ghz = "Connection weak on 2.4 GHz";

    /// <summary>As <see cref="WeakHintOn24Ghz"/>, with the suggestion the user can still act on.</summary>
    public const string WeakHintOn24GhzTrySmooth = "Connection weak on 2.4 GHz — try Smooth";

    /// <summary>
    /// True when the radio itself *measures* weak — RSSI or PHY link speed at or below the
    /// thresholds above. Requires an actual Wi-Fi association: an unknown link (non-Android host,
    /// wired, failed read) never produces a radio claim, and the two sentinel guards mean a platform
    /// that reported no RSSI or no link speed is not weak by default.
    /// <para>
    /// The band is deliberately NOT an arm of this test. A 2.4 GHz association at -45 dBm and
    /// 150 Mbit/s is not a weak radio, and calling it one made the viewer tell an operator
    /// "2.4 GHz / weak signal — switch to Smooth" about a perfectly good AP whenever the *sender*
    /// went quiet: a false causal claim, and a suggestion that costs a visible reconnect and cannot
    /// help. 2.4 GHz's real failure mode is airtime congestion, which is invisible in both RSSI and
    /// PHY rate, so the band survives as a qualifier in the copy rather than as a cause.
    /// </para>
    /// </summary>
    public static bool IsWeakLink(NetworkLinkSnapshot link) =>
        link.IsWifi
        && ((link.Rssi != NetworkLinkSnapshot.UnknownRssi && link.Rssi <= WeakRssiDbm)
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
    /// show. Three tiers, ordered by how strong a claim each makes:
    /// <list type="number">
    /// <item>the radio measures weak and frames were actually being lost — name the band and the
    /// signal ("2.4 GHz / weak signal — switch to Smooth");</item>
    /// <item>frames were being lost on a 2.4 GHz radio that measures fine — name the band as a fact
    /// and claim no cause ("Connection weak on 2.4 GHz — try Smooth"), because 2.4 GHz congestion
    /// does not show up in RSSI or PHY rate;</item>
    /// <item>otherwise the generic copy, which blames nothing.</item>
    /// </list>
    /// <paramref name="sawDropEvidence"/> is what keeps tiers 1 and 2 honest. A receiver that is up
    /// while the sender sends nothing is weak by the fps arm alone with a 0 % drop rate, and the
    /// Wi-Fi has nothing to do with it; a Stalled sample therefore needs no special case here. Pass
    /// <c>State.SawDropEvidence</c>, which latches across the whole run so a spiky rate cannot make
    /// the wording flicker second to second.
    /// The link only ever changes the wording — this class never calls SetQualityProfile and nothing
    /// downstream of it does either.
    /// </summary>
    public static string? HintText(
        bool isHintActive,
        QualityProfile profile,
        NetworkLinkSnapshot link,
        bool sawDropEvidence)
    {
        if (!isHintActive)
            return null;

        if (!sawDropEvidence)
            return HintText(isHintActive, profile);

        if (IsWeakLink(link))
        {
            // On Smooth the suggestion is dropped: the user is already on the lowest tier the
            // standard SDK exposes.
            var prefix = $"{LinkDescription(link)} / weak signal";
            return profile == QualityProfile.Smooth ? prefix : prefix + " — switch to Smooth";
        }

        if (link.IsWifi && link.Band == WifiBand.TwoPointFourGhz)
            return profile == QualityProfile.Smooth ? WeakHintOn24Ghz : WeakHintOn24GhzTrySmooth;

        return HintText(isHintActive, profile);
    }

    public static bool IsWeak(float fps, float dropPercent) =>
        fps < WeakFpsThreshold || dropPercent > WeakDropPercentThreshold;

    /// <summary>
    /// A sample that positively demonstrates a healthy stream. The fps term is <c>&gt;=</c> where
    /// <see cref="IsWeak"/>'s is <c>&lt;</c>, so the two partition the fps axis: a source sitting
    /// exactly on the threshold used to be neither weak nor good forever, which under
    /// <see cref="Next"/> means an active hint could never clear at all. The drop band between
    /// <see cref="GoodDropPercentThreshold"/> and <see cref="WeakDropPercentThreshold"/> is the one
    /// intentional dead zone.
    /// </summary>
    public static bool IsGood(float fps, float dropPercent) =>
        fps >= GoodFpsThreshold && dropPercent < GoodDropPercentThreshold;

    /// <summary>
    /// Feeds one sample and returns the new state. The hysteresis is deliberately asymmetric, and
    /// the asymmetry is the round-2 #415 fix: now that <c>dropPercent</c> is a true per-interval
    /// rate rather than a lifetime average it is spiky by construction, so single seconds in the
    /// 10-30 % dead band are normal on a link that has already recovered. A dead-band sample
    /// therefore
    /// <list type="bullet">
    /// <item>resets the weak run — it proves that second was below the weak threshold, so a blip
    /// can still only ever reset the show run and never flip the hint; and</item>
    /// <item>leaves the good run untouched — it does not demonstrate health, so it must not destroy
    /// evidence of health. Before this, one 14 % second every few samples zeroed the clear run and
    /// an active hint stayed up forever on a link averaging ~7 % loss.</item>
    /// </list>
    /// Smoothing the value itself was rejected: the bridge must keep printing the honest interval
    /// rate so <c>drop=</c> still equals <c>dDropped/dTotal</c> in the NdiStats line.
    /// </summary>
    public static State Next(State state, float fps, float dropPercent)
    {
        if (IsWeak(fps, dropPercent))
        {
            var weak = state.WeakRun + 1;
            return new State(
                weak,
                0,
                state.IsHintActive || weak >= WeakSamplesToShow,
                // Latched for the life of the hint: did any weak second actually lose frames? The
                // radio may only be named as a cause when it did — see the HintText overload that
                // takes a link.
                state.SawDropEvidence || dropPercent > 0f);
        }

        if (IsGood(fps, dropPercent))
        {
            var good = state.GoodRun + 1;
            var active = state.IsHintActive && good < GoodSamplesToClear;
            return new State(0, good, active, active && state.SawDropEvidence);
        }

        // A dead-band sample abandons the weak run; the evidence it gathered must not outlive it,
        // or a later fps-only hint would be attributed to the radio.
        return new State(0, state.GoodRun, state.IsHintActive, state.IsHintActive && state.SawDropEvidence);
    }

    /// <summary>Hint text for the active profile, or null when no hint should show.</summary>
    public static string? HintText(bool isHintActive, QualityProfile profile) => !isHintActive
        ? null
        : profile == QualityProfile.Smooth ? WeakHint : WeakHintTrySmooth;

    /// <summary>
    /// Hint hysteresis state. <c>SawDropEvidence</c> records whether any weak sample in the run that
    /// raised the current hint actually lost frames; it gates the radio attribution in the
    /// link-aware <c>HintText</c> overload so a source that is simply sending nothing is never
    /// blamed on the Wi-Fi. It is a trailing parameter with a default, so existing three-argument
    /// construction keeps compiling.
    /// </summary>
    public readonly record struct State(int WeakRun, int GoodRun, bool IsHintActive, bool SawDropEvidence = false)
    {
        public static readonly State Idle = new(0, 0, false);
    }
}
