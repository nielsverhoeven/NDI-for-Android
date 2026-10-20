using System.Net.Sockets;
using NdiForAndroid.Features.Ptz.Models;
using NdiForAndroid.Features.Ptz.Services;
using NdiForAndroid.Tests.Features.Ptz.Fakes;
using Xunit;

namespace NdiForAndroid.Tests.Features.Ptz;

public class ViscaPtzControllerTests
{
    private static readonly PtzEndpoint Endpoint = new("127.0.0.1", 5678);
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(200);

    private static readonly byte[] Ack1 = { 0x90, 0x41, 0xFF };
    private static readonly byte[] Completion1 = { 0x90, 0x51, 0xFF };
    private static readonly byte[] SocketOneError = { 0x90, 0x61, 0x41, 0xFF };
    private static readonly byte[] SocketZeroError = { 0x90, 0x60, 0x41, 0xFF };
    private static readonly byte[] UnknownFrame = { 0x90, 0x38, 0xFF };

    [Fact]
    public async Task PanTiltAsync_SendsEncodedCommand_ReturnsTrueOnAck()
    {
        var transport = new FakeViscaTransport();
        var sut = new ViscaPtzController(transport, Endpoint);

        var result = await sut.PanTiltAsync(1f, 0f);

        Assert.True(result);
        Assert.Single(transport.SentCommands);
        Assert.Equal(ViscaCommandEncoder.PanTiltDrive(1f, 0f), transport.SentCommands[0]);
        Assert.Equal(PtzLinkState.Connected, sut.LinkState);
    }

    [Fact]
    public async Task PanTiltAsync_ConnectFails_ReturnsFalseAndSetsErrorState()
    {
        var transport = new FakeViscaTransport();
        transport.EnqueueConnectFailure(new InvalidOperationException("connect failed"));
        var sut = new ViscaPtzController(transport, Endpoint);

        var result = await sut.PanTiltAsync(1f, 0f);

        Assert.False(result);
        Assert.Equal(PtzLinkState.Error, sut.LinkState);
        Assert.NotNull(sut.LastError);
    }

    [Fact]
    public async Task PanTiltAsync_ReceiveTimesOut_ReturnsFalseAndSetsErrorState()
    {
        var transport = new FakeViscaTransport();
        transport.EnqueueReceiveFailure(new TaskCanceledException("simulated timeout"));
        var sut = new ViscaPtzController(transport, Endpoint);

        var result = await sut.PanTiltAsync(1f, 0f);

        Assert.False(result);
        Assert.Equal(PtzLinkState.Error, sut.LinkState);
    }

    [Fact]
    public async Task PanTiltAsync_SendThrowsUnexpectedException_ReturnsFalseInsteadOfThrowing()
    {
        var transport = new FakeViscaTransport();
        transport.EnqueueSendFailure(new InvalidOperationException("socket faulted"));
        var sut = new ViscaPtzController(transport, Endpoint);

        var result = await sut.PanTiltAsync(1f, 0f);

        Assert.False(result);
        Assert.Equal(PtzLinkState.Error, sut.LinkState);
    }

    [Fact]
    public async Task PanTiltAsync_ReceivesErrorFrame_ReturnsFalseButStaysConnected()
    {
        var transport = new FakeViscaTransport();
        transport.EnqueueReply(new byte[] { 0x90, 0x60, 0x02, 0xFF });
        var sut = new ViscaPtzController(transport, Endpoint);

        var result = await sut.PanTiltAsync(1f, 0f);

        Assert.False(result);
        Assert.Equal(PtzLinkState.Connected, sut.LinkState);
        Assert.NotNull(sut.LastError);
    }

    [Fact]
    public async Task PanTiltAsync_CalledTwiceWhileConnected_ReusesConnection()
    {
        var transport = new FakeViscaTransport();
        var sut = new ViscaPtzController(transport, Endpoint);

        await sut.PanTiltAsync(1f, 0f);
        await sut.PanTiltAsync(-1f, 0f);

        Assert.Equal(1, transport.ConnectCount);
    }

    [Fact]
    public async Task PanTiltAsync_FailsOnOpenConnection_RetriesOnceAndReconnects()
    {
        var transport = new FakeViscaTransport();
        var sut = new ViscaPtzController(transport, Endpoint);
        await sut.PanTiltAsync(1f, 0f);

        transport.EnqueueReceiveFailure(new IOException("peer closed"));
        var result = await sut.PanTiltAsync(0f, 1f);

        Assert.True(result);
        Assert.Equal(2, transport.ConnectCount);
        Assert.Equal(1, transport.DisconnectCount);
    }

    [Fact]
    public async Task PanTiltAsync_FailsOnFreshConnection_DoesNotRetry()
    {
        var transport = new FakeViscaTransport();
        transport.EnqueueReceiveFailure(new IOException("boom"));
        var sut = new ViscaPtzController(transport, Endpoint);

        var result = await sut.PanTiltAsync(1f, 0f);

        Assert.False(result);
        Assert.Equal(1, transport.ConnectCount);
    }

    [Fact]
    public async Task LinkStateChanged_FiresOncePerActualTransition()
    {
        var transport = new FakeViscaTransport();
        var sut = new ViscaPtzController(transport, Endpoint);
        var transitions = new List<PtzLinkState>();
        sut.LinkStateChanged += (_, state) => transitions.Add(state);

        await sut.PanTiltAsync(1f, 0f);
        await sut.PanTiltAsync(0f, 1f);

        Assert.Equal(new[] { PtzLinkState.Connecting, PtzLinkState.Connected }, transitions);
    }

    [Fact]
    public async Task ShutdownAsync_DisconnectsAndSetsDisconnectedState()
    {
        var transport = new FakeViscaTransport();
        var sut = new ViscaPtzController(transport, Endpoint);
        await sut.PanTiltAsync(1f, 0f);

        await sut.ShutdownAsync();

        Assert.Equal(1, transport.DisconnectCount);
        Assert.Equal(PtzLinkState.Disconnected, sut.LinkState);
    }

    [Fact]
    public void LinkState_Initially_IsDisconnected()
    {
        var sut = new ViscaPtzController(new FakeViscaTransport(), Endpoint);

        Assert.Equal(PtzLinkState.Disconnected, sut.LinkState);
    }

    [Fact]
    public async Task PanTiltAsync_StaleCompletionOfThePreviousCommand_IsSkipped()
    {
        var transport = new FakeViscaTransport();
        transport.EnqueueReply(Ack1);
        var sut = new ViscaPtzController(transport, Endpoint);
        var first = await sut.PanTiltAsync(1f, 0f);
        transport.EnqueueReply(Completion1);
        transport.EnqueueReply(Ack1);

        var second = await sut.PanTiltAsync(0f, 1f);

        Assert.True(first);
        Assert.True(second);
        Assert.Equal(2, transport.SentCommands.Count);
        Assert.Equal(0, transport.PendingReplyCount);
    }

    [Fact]
    public async Task PanTiltAsync_LateSocketErrorOfThePreviousCommand_IsNotChargedToTheCurrentCommand()
    {
        var transport = new FakeViscaTransport();
        transport.EnqueueReply(Ack1);
        var sut = new ViscaPtzController(transport, Endpoint);
        await sut.PanTiltAsync(1f, 0f);
        transport.EnqueueReply(SocketOneError);
        transport.EnqueueReply(Ack1);

        var second = await sut.PanTiltAsync(0f, 1f);

        Assert.True(second);
        Assert.Null(sut.LastError);
    }

    [Fact]
    public async Task PanTiltAsync_ErrorWithSocketZero_FailsTheCurrentCommandAndIsNotResent()
    {
        var transport = new FakeViscaTransport();
        transport.EnqueueReply(Ack1);
        var sut = new ViscaPtzController(transport, Endpoint);
        await sut.PanTiltAsync(1f, 0f);
        transport.EnqueueReply(SocketZeroError);

        var second = await sut.PanTiltAsync(0f, 1f);

        Assert.False(second);
        Assert.Equal(PtzLinkState.Connected, sut.LinkState);
        Assert.Equal("VISCA error 0x41", sut.LastError);
        Assert.Equal(2, transport.SentCommands.Count);
        Assert.Equal(1, transport.ConnectCount);
    }

    [Fact]
    public async Task PanTiltAsync_ErrorOnASocketWithNothingPending_FailsTheCurrentCommand()
    {
        var transport = new FakeViscaTransport();
        transport.EnqueueReply(SocketOneError);
        var sut = new ViscaPtzController(transport, Endpoint);

        var result = await sut.PanTiltAsync(1f, 0f);

        Assert.False(result);
    }

    [Fact]
    public async Task PanTiltAsync_CompletionWithoutAnAck_IsTheCurrentReply()
    {
        var transport = new FakeViscaTransport();
        transport.EnqueueReply(Completion1);
        var sut = new ViscaPtzController(transport, Endpoint);

        var result = await sut.PanTiltAsync(1f, 0f);

        Assert.True(result);
    }

    [Fact]
    public async Task PanTiltAsync_UnknownFrameBeforeTheReply_IsSkipped()
    {
        var transport = new FakeViscaTransport();
        transport.EnqueueReply(UnknownFrame);
        transport.EnqueueReply(Ack1);
        var sut = new ViscaPtzController(transport, Endpoint);

        var result = await sut.PanTiltAsync(1f, 0f);

        Assert.True(result);
    }

    [Fact]
    public async Task PanTiltAsync_TooManyUnexpectedFrames_FailsAsATransportFailure()
    {
        var transport = new FakeViscaTransport();
        for (var i = 0; i < 8; i++)
            transport.EnqueueReply(UnknownFrame);
        var sut = new ViscaPtzController(transport, Endpoint);

        var result = await sut.PanTiltAsync(1f, 0f);

        Assert.False(result);
        Assert.Equal(PtzLinkState.Error, sut.LinkState);
        Assert.Equal("Camera sent unexpected replies.", sut.LastError);
    }

    [Fact]
    public async Task PanTiltAsync_AfterAReconnect_PendingSocketsAreCleared()
    {
        var transport = new FakeViscaTransport();
        transport.EnqueueReply(Ack1);
        var sut = new ViscaPtzController(transport, Endpoint, commandTimeout: ShortTimeout);
        await sut.PanTiltAsync(1f, 0f);
        transport.EnqueueReceiveFailure(new IOException("peer closed"));
        transport.EnqueueReply(Completion1);
        transport.EnqueueReceiveHang();

        var second = await sut.PanTiltAsync(0f, 1f);

        Assert.True(second);
        Assert.Equal(2, transport.ConnectCount);
    }

    [Fact]
    public async Task ShutdownAsync_ClearsPendingSockets()
    {
        var transport = new FakeViscaTransport();
        transport.EnqueueReply(Ack1);
        var sut = new ViscaPtzController(transport, Endpoint, commandTimeout: ShortTimeout);
        await sut.PanTiltAsync(1f, 0f);
        await sut.ShutdownAsync();
        transport.EnqueueReply(Completion1);
        transport.EnqueueReceiveHang();

        var second = await sut.PanTiltAsync(0f, 1f);

        Assert.True(second);
    }

    [Fact]
    public async Task PanTiltAsync_ManyAckAndCompletionReplies_StayAlignedThenTimeOutWhenSilent()
    {
        var transport = new FakeViscaTransport();
        var sut = new ViscaPtzController(transport, Endpoint, commandTimeout: ShortTimeout);
        for (var i = 1; i <= 20; i++)
        {
            transport.EnqueueReply(Ack1);
            transport.EnqueueReply(Completion1);

            Assert.True(await sut.PanTiltAsync(1f, 0f), $"command {i} should succeed");
        }

        transport.EnqueueReceiveHang();
        transport.EnqueueReceiveHang();
        var silent = await sut.PanTiltAsync(1f, 0f);

        Assert.False(silent);
        Assert.Equal("Camera did not answer (timed out).", sut.LastError);
        Assert.Equal(2, transport.ConnectCount);
    }

    [Fact]
    public async Task PanTiltAsync_ConnectTimesOut_ReportsNoConnectionToTheEndpoint()
    {
        var transport = new FakeViscaTransport();
        transport.EnqueueConnectHang();
        var sut = new ViscaPtzController(transport, Endpoint, connectTimeout: TimeSpan.FromMilliseconds(100));

        var result = await sut.PanTiltAsync(1f, 0f);

        Assert.False(result);
        Assert.Equal("No connection to 127.0.0.1:5678 (timed out).", sut.LastError);
    }

    [Fact]
    public async Task PanTiltAsync_CallerTokenFiresDuringConnect_StillReportsNoConnection()
    {
        var transport = new FakeViscaTransport();
        transport.EnqueueConnectHang();
        var sut = new ViscaPtzController(transport, Endpoint, connectTimeout: TimeSpan.FromSeconds(5));
        using var callerCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var result = await sut.PanTiltAsync(1f, 0f, callerCts.Token);

        Assert.False(result);
        Assert.Equal("No connection to 127.0.0.1:5678 (timed out).", sut.LastError);
    }

    [Fact]
    public async Task PanTiltAsync_ConnectionRefused_ReportsCannotReach()
    {
        var transport = new FakeViscaTransport();
        transport.EnqueueConnectFailure(new SocketException((int)SocketError.ConnectionRefused));
        var sut = new ViscaPtzController(transport, Endpoint);

        await sut.PanTiltAsync(1f, 0f);

        Assert.Equal("Cannot reach 127.0.0.1:5678 (connection refused).", sut.LastError);
    }

    [Fact]
    public async Task PanTiltAsync_HostNotFound_ReportsCannotReach()
    {
        var transport = new FakeViscaTransport();
        transport.EnqueueConnectFailure(new SocketException((int)SocketError.HostNotFound));
        var sut = new ViscaPtzController(transport, Endpoint);

        await sut.PanTiltAsync(1f, 0f);

        Assert.Equal("Cannot reach 127.0.0.1:5678 (host not found).", sut.LastError);
    }

    [Fact]
    public async Task PanTiltAsync_CommandTimesOut_ReportsCameraDidNotAnswer()
    {
        var transport = new FakeViscaTransport();
        transport.EnqueueReceiveHang();
        var sut = new ViscaPtzController(transport, Endpoint, commandTimeout: TimeSpan.FromMilliseconds(100));

        await sut.PanTiltAsync(1f, 0f);

        Assert.Equal("Camera did not answer (timed out).", sut.LastError);
    }

    [Fact]
    public async Task PanTiltAsync_PeerClosesTheConnection_ReportsConnectionLost()
    {
        var transport = new FakeViscaTransport();
        transport.EnqueueReceiveFailure(new IOException("peer closed"));
        var sut = new ViscaPtzController(transport, Endpoint);

        await sut.PanTiltAsync(1f, 0f);

        Assert.Equal("Connection to the camera was lost.", sut.LastError);
    }
}
