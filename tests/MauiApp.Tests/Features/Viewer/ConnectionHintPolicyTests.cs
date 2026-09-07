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
    [InlineData(15f, 9.9f, false)]
    [InlineData(30f, 10f, false)]
    public void IsGood_Theory(float fps, float dropPercent, bool expected)
    {
        Assert.Equal(expected, ConnectionHintPolicy.IsGood(fps, dropPercent));
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
    public void Next_InBetweenSample_ResetsWeakRun()
    {
        var state = ConnectionHintPolicy.State.Idle;
        for (var i = 0; i < 4; i++)
            state = ConnectionHintPolicy.Next(state, 5f, 40f);

        state = ConnectionHintPolicy.Next(state, 15f, 20f);

        Assert.Equal(0, state.WeakRun);
        Assert.Equal(0, state.GoodRun);
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
    public void Next_WhileActive_InBetweenSampleKeepsHint()
    {
        var state = new ConnectionHintPolicy.State(0, 3, true);

        state = ConnectionHintPolicy.Next(state, 15f, 20f);

        Assert.True(state.IsHintActive);
        Assert.Equal(0, state.WeakRun);
        Assert.Equal(0, state.GoodRun);
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
}
