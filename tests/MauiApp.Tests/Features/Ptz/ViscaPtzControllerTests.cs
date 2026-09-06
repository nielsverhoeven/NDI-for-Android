using NdiForAndroid.Features.Ptz.Models;
using NdiForAndroid.Features.Ptz.Services;
using NdiForAndroid.Tests.Features.Ptz.Fakes;
using Xunit;

namespace NdiForAndroid.Tests.Features.Ptz;

public class ViscaPtzControllerTests
{
    private static readonly PtzEndpoint Endpoint = new("127.0.0.1", 5678);

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
}
