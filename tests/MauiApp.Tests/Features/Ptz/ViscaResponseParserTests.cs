using NdiForAndroid.Features.Ptz.Services;
using Xunit;

namespace NdiForAndroid.Tests.Features.Ptz;

public class ViscaResponseParserTests
{
    [Fact]
    public void Parse_AckFrame_ReturnsAckWithSocket()
    {
        var response = ViscaResponseParser.Parse(new byte[] { 0x90, 0x41, 0xFF });

        Assert.Equal(ViscaResponseKind.Ack, response.Kind);
        Assert.Equal(1, response.Socket);
        Assert.Null(response.ErrorCode);
    }

    [Fact]
    public void Parse_CompletionFrame_ReturnsCompletionWithSocket()
    {
        var response = ViscaResponseParser.Parse(new byte[] { 0x90, 0x51, 0xFF });

        Assert.Equal(ViscaResponseKind.Completion, response.Kind);
        Assert.Equal(1, response.Socket);
    }

    [Fact]
    public void Parse_ErrorFrame_ReturnsErrorWithCode()
    {
        var response = ViscaResponseParser.Parse(new byte[] { 0x90, 0x60, 0x02, 0xFF });

        Assert.Equal(ViscaResponseKind.Error, response.Kind);
        Assert.Equal((byte)0x02, response.ErrorCode);
    }

    [Fact]
    public void Parse_TooShortFrame_ReturnsUnknown()
    {
        var response = ViscaResponseParser.Parse(new byte[] { 0x90, 0xFF });

        Assert.Equal(ViscaResponseKind.Unknown, response.Kind);
    }

    [Fact]
    public void Parse_NotTerminatedByFF_ReturnsUnknown()
    {
        var response = ViscaResponseParser.Parse(new byte[] { 0x90, 0x41, 0x00 });

        Assert.Equal(ViscaResponseKind.Unknown, response.Kind);
    }

    [Fact]
    public void ParseAll_ConcatenatedAckAndCompletion_ReturnsBothInOrder()
    {
        var responses = ViscaResponseParser.ParseAll(new byte[] { 0x90, 0x41, 0xFF, 0x90, 0x51, 0xFF });

        Assert.Equal(2, responses.Count);
        Assert.Equal(ViscaResponseKind.Ack, responses[0].Kind);
        Assert.Equal(ViscaResponseKind.Completion, responses[1].Kind);
    }

    [Fact]
    public void ParseAll_TrailingIncompleteFrame_IsDropped()
    {
        var responses = ViscaResponseParser.ParseAll(new byte[] { 0x90, 0x41, 0xFF, 0x90, 0x51 });

        Assert.Single(responses);
        Assert.Equal(ViscaResponseKind.Ack, responses[0].Kind);
    }

    [Fact]
    public void ParseAll_FedOneByteAtATime_YieldsFrameOnlyOnceTerminatorArrives()
    {
        var source = new byte[] { 0x90, 0x41, 0xFF };

        for (var length = 1; length < source.Length; length++)
        {
            var responses = ViscaResponseParser.ParseAll(source.AsSpan(0, length));
            Assert.Empty(responses);
        }

        var complete = ViscaResponseParser.ParseAll(source);
        Assert.Single(complete);
        Assert.Equal(ViscaResponseKind.Ack, complete[0].Kind);
    }
}
