using NdiForAndroid.Features.Viewer;
using NdiForAndroid.NdiBridge;
using Xunit;

namespace NdiForAndroid.Tests.Features.Viewer;

public class ConnectionHintPolicyTests
{
    [Theory]
    [InlineData(14.9f, 0f, true)]
    [InlineData(15f, 0f, false)]
    [InlineData(30f, 30.1f, true)]
    [InlineData(30f, 30f, false)]
    public void IsWeak_Theory(float fps, float dropPercent, bool expected)
    {
        Assert.Equal(expected, ConnectionHintPolicy.IsWeak(fps, dropPercent));
    }

    [Theory]
    [InlineData(15.1f, 9.9f, true)]
    [InlineData(15f, 9.9f, true)]
    [InlineData(14.9f, 9.9f, false)]
    [InlineData(14.9f, 0f, false)]
    [InlineData(30f, 10f, true)]
    [InlineData(30f, 30f, true)]
    [InlineData(30f, 30.1f, false)]
    public void IsGood_Theory(float fps, float dropPercent, bool expected)
    {
        // IsGood is the complement of IsWeak: no value dead band (#415, 2026-10-10).
        Assert.Equal(expected, ConnectionHintPolicy.IsGood(fps, dropPercent));
        Assert.NotEqual(ConnectionHintPolicy.IsWeak(fps, dropPercent), ConnectionHintPolicy.IsGood(fps, dropPercent));
    }

    [Fact]
    public void Next_FourWeakSamples_DoNotActivate()
    {
        var state = ConnectionHintPolicy.State.Idle;
        for (var i = 0; i < 4; i++)
            state = ConnectionHintPolicy.Next(state, 5f, 40f);

        Assert.False(state.IsHintActive);
        Assert.Equal(4, state.WeakRun);
    }

    [Fact]
    public void Next_FifthWeakSample_Activates()
    {
        var state = ConnectionHintPolicy.State.Idle;
        for (var i = 0; i < 5; i++)
            state = ConnectionHintPolicy.Next(state, 5f, 40f);

        Assert.True(state.IsHintActive);
    }

    [Fact]
    public void Next_NonWeakSample_ResetsWeakRun_AndStartsTheGoodRun()
    {
        var state = ConnectionHintPolicy.State.Idle;
        for (var i = 0; i < 4; i++)
            state = ConnectionHintPolicy.Next(state, 5f, 40f);

        state = ConnectionHintPolicy.Next(state, 15f, 20f);

        Assert.Equal(0, state.WeakRun);
        Assert.Equal(1, state.GoodRun);
        Assert.False(state.IsHintActive);
    }

    [Fact]
    public void Next_WhileActive_FourGoodSamplesKeepHint_FifthClears()
    {
        var state = new ConnectionHintPolicy.State(0, 0, true);
        for (var i = 0; i < 4; i++)
            state = ConnectionHintPolicy.Next(state, 30f, 0f);

        Assert.True(state.IsHintActive);

        state = ConnectionHintPolicy.Next(state, 30f, 0f);

        Assert.False(state.IsHintActive);
    }

    [Fact]
    public void Next_WhileActive_WeakSampleResetsGoodRun()
    {
        var state = new ConnectionHintPolicy.State(0, 4, true);

        state = ConnectionHintPolicy.Next(state, 5f, 0f);

        Assert.Equal(0, state.GoodRun);
        Assert.True(state.IsHintActive);
    }

    [Fact]
    public void Next_WhileActive_NonWeakSample_AdvancesTheGoodRun()
    {
        var state = new ConnectionHintPolicy.State(0, 3, true);

        state = ConnectionHintPolicy.Next(state, 15f, 20f);

        // #415 2026-10-10: a 20 % second is not weak, so it counts towards clearing. The former
        // dead band held an active hint up forever at the tablet's 1080p60 steady state.
        Assert.True(state.IsHintActive);
        Assert.Equal(0, state.WeakRun);
        Assert.Equal(4, state.GoodRun);
    }

    [Fact]
    public void HintText_Inactive_IsNull()
    {
        Assert.Null(ConnectionHintPolicy.HintText(false, QualityProfile.Balanced));
    }

    [Fact]
    public void HintText_Active_Balanced_SaysTrySmooth()
    {
        Assert.Equal("Connection weak — try Smooth", ConnectionHintPolicy.HintText(true, QualityProfile.Balanced));
    }

    [Fact]
    public void HintText_Active_Smooth_OmitsSuggestion()
    {
        Assert.Equal("Connection weak", ConnectionHintPolicy.HintText(true, QualityProfile.Smooth));
    }

    [Fact]
    public void Next_SpikyButRecoveringLink_StillClearsTheHint()
    {
        // Five genuinely weak seconds raise the hint...
        var state = ConnectionHintPolicy.State.Idle;
        foreach (var drop in new[] { 35f, 35f, 35f, 35f, 35f })
            state = ConnectionHintPolicy.Next(state, 30f, drop);
        Assert.True(state.IsHintActive);

        // ...then the link recovers to ~7 % average, with single seconds in the 10-30 % dead band.
        // Before the fix each of those zeroed the clear run and the hint never cleared at all.
        foreach (var drop in new[] { 4f, 14f, 6f, 3f, 12f, 5f, 11f, 4f })
            state = ConnectionHintPolicy.Next(state, 30f, drop);

        Assert.False(state.IsHintActive);
    }

    [Theory]
    [InlineData(35f, 5f)]   // weak second alternating with a good one
    [InlineData(35f, 14f)]  // weak second alternating with a dead-band one
    public void Next_JitteryLink_NeverRaisesTheHint(float weakDrop, float otherDrop)
    {
        var state = ConnectionHintPolicy.State.Idle;

        for (var i = 0; i < 40; i++)
            state = ConnectionHintPolicy.Next(state, 30f, i % 2 == 0 ? weakDrop : otherDrop);

        Assert.False(state.IsHintActive);
    }

    [Fact]
    public void Next_LinkBelowTheWeakThreshold_ClearsAnActiveHintAfterFiveSamples()
    {
        // A link losing 20 % is below the level the detector would raise a hint for; a drop level
        // that never justified showing the hint must not keep it alive (#415, 2026-10-10).
        var state = new ConnectionHintPolicy.State(0, 0, true);

        for (var i = 0; i < 4; i++)
            state = ConnectionHintPolicy.Next(state, 30f, 20f);
        Assert.True(state.IsHintActive);

        state = ConnectionHintPolicy.Next(state, 30f, 20f);
        Assert.False(state.IsHintActive);
    }

    [Fact]
    public void Next_ExactlyAtTheFpsThreshold_CanStillClearTheHint()
    {
        var state = new ConnectionHintPolicy.State(0, 0, true);

        for (var i = 0; i < ConnectionHintPolicy.GoodSamplesToClear; i++)
            state = ConnectionHintPolicy.Next(state, ConnectionHintPolicy.WeakFpsThreshold, 0f);

        Assert.False(state.IsHintActive);
    }

    [Fact]
    public void Next_HintRaisedOnTheFpsArm_ClearsAtTheTabletsSteadyState()
    {
        // Device 2026-10-10: sender stopped video (Stalled, fps 0, nothing dropped) — the hint
        // appeared after ~7 s on the fps arm. Video then resumed at 1080p60: fps 49, 18 % of received
        // frames discarded by the receiver. The hint stayed up for 70 s because 18 % sat in the
        // dead band; it must clear after five non-weak samples.
        var state = ConnectionHintPolicy.State.Idle;
        for (var i = 0; i < 7; i++)
            state = ConnectionHintPolicy.Next(state, 0f, 0f);

        Assert.True(state.IsHintActive);
        Assert.False(state.SawDropEvidence);

        for (var i = 0; i < 5; i++)
            state = ConnectionHintPolicy.Next(state, 49f, 18f);

        Assert.False(state.IsHintActive);
    }

    [Fact]
    public void Next_HintRaisedOnTheDropArm_ClearsOnceTheRateIsBelowTheThreshold()
    {
        // A genuine > 30 % burst raises the hint with drop evidence; once the rate is back below the
        // threshold (even at the tablet's 18 % steady state) the hint clears and the evidence goes.
        var state = ConnectionHintPolicy.State.Idle;
        for (var i = 0; i < 5; i++)
            state = ConnectionHintPolicy.Next(state, 49f, 40f);

        Assert.True(state.IsHintActive);
        Assert.True(state.SawDropEvidence);

        for (var i = 0; i < 5; i++)
            state = ConnectionHintPolicy.Next(state, 49f, 18f);

        Assert.False(state.IsHintActive);
        Assert.False(state.SawDropEvidence);
    }

    [Fact]
    public void Next_LinkHoveringOnTheThreshold_FlipsAtMostOncePerFiveSamples()
    {
        // 5 s just above the threshold, 5 s just below, repeated. Without value hysteresis the hint
        // flaps, but only ever on the fifth sample of each phase — never in between.
        var state = ConnectionHintPolicy.State.Idle;

        for (var i = 0; i < 40; i++)
        {
            var previous = state.IsHintActive;
            var drop = (i / 5) % 2 == 0 ? 31f : 29f;

            state = ConnectionHintPolicy.Next(state, 30f, drop);

            if ((i + 1) % 5 == 0)
                Assert.NotEqual(previous, state.IsHintActive);
            else
                Assert.Equal(previous, state.IsHintActive);
        }
    }

    [Fact]
    public void Next_WeakWithoutDrops_LatchesNoDropEvidence()
    {
        // Receiver up, source silent: weak by the fps arm, nothing lost. The radio is not implicated.
        var state = ConnectionHintPolicy.State.Idle;

        for (var i = 0; i < 5; i++)
            state = ConnectionHintPolicy.Next(state, 0f, 0f);

        Assert.True(state.IsHintActive);
        Assert.False(state.SawDropEvidence);
    }

    [Fact]
    public void Next_WeakWithDrops_LatchesEvidence_AndForgetsItWhenTheHintClears()
    {
        var state = ConnectionHintPolicy.State.Idle;
        for (var i = 0; i < 5; i++)
            state = ConnectionHintPolicy.Next(state, 5f, 40f);
        Assert.True(state.SawDropEvidence);

        for (var i = 0; i < 5; i++)
            state = ConnectionHintPolicy.Next(state, 30f, 0f);

        Assert.False(state.IsHintActive);
        Assert.False(state.SawDropEvidence);
    }

    [Fact]
    public void Next_AbandonedWeakRun_DoesNotCarryDropEvidenceIntoALaterHint()
    {
        // A jittery link: every weak second that lost frames is followed by a dead-band second, so
        // no hint is ever raised. When the source then goes silent, the fps-only hint must not
        // inherit the drop evidence of those abandoned runs.
        var state = ConnectionHintPolicy.State.Idle;
        for (var i = 0; i < 20; i++)
        {
            state = ConnectionHintPolicy.Next(state, 5f, 35f);
            state = ConnectionHintPolicy.Next(state, 30f, 14f);
        }

        Assert.False(state.IsHintActive);

        for (var i = 0; i < 5; i++)
            state = ConnectionHintPolicy.Next(state, 0f, 0f);

        Assert.True(state.IsHintActive);
        Assert.False(state.SawDropEvidence);
    }
}
