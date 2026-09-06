using System.Diagnostics;
using NdiForAndroid.Features.Ptz.Services;
using NdiForAndroid.Tests.Features.Ptz.Fakes;
using Xunit;

namespace NdiForAndroid.Tests.Features.Ptz;

public class ViscaTcpTransportLoopbackTests : IAsyncLifetime
{
    private readonly LoopbackViscaCamera _camera = new();

    public Task InitializeAsync() => _camera.InitializeAsync();

    public Task DisposeAsync() => _camera.DisposeAsync();

    [Fact]
    public async Task ConnectAsync_ToLoopbackCamera_Succeeds()
    {
        var transport = new ViscaTcpTransport();

        await transport.ConnectAsync("127.0.0.1", _camera.Port);

        Assert.True(transport.IsConnected);
        await transport.DisconnectAsync();
    }

    [Fact]
    public async Task SendAndReceive_RespondMode_RoundTripsAck()
    {
        var transport = new ViscaTcpTransport();
        await transport.ConnectAsync("127.0.0.1", _camera.Port);

        await transport.SendAsync(ViscaCommandEncoder.AutoFocus());
        var reply = await transport.ReceiveFrameAsync();

        Assert.Equal(new byte[] { 0x90, 0x41, 0xFF }, reply);
        await transport.DisconnectAsync();
    }

    [Fact]
    public async Task SendAndReceive_SplitAckMode_ReassemblesAcrossReads()
    {
        _camera.Mode = LoopbackViscaCameraMode.SplitAck;
        var transport = new ViscaTcpTransport();
        await transport.ConnectAsync("127.0.0.1", _camera.Port);

        await transport.SendAsync(ViscaCommandEncoder.AutoFocus());
        var reply = await transport.ReceiveFrameAsync();

        Assert.Equal(new byte[] { 0x90, 0x41, 0xFF }, reply);
        await transport.DisconnectAsync();
    }

    [Fact]
    public async Task SendAndReceive_ErrorMode_ReturnsErrorFrame()
    {
        _camera.Mode = LoopbackViscaCameraMode.Error;
        var transport = new ViscaTcpTransport();
        await transport.ConnectAsync("127.0.0.1", _camera.Port);

        await transport.SendAsync(ViscaCommandEncoder.AutoFocus());
        var reply = await transport.ReceiveFrameAsync();

        Assert.Equal(new byte[] { 0x90, 0x60, 0x02, 0xFF }, reply);
        await transport.DisconnectAsync();
    }

    [Fact]
    public async Task ReceiveFrameAsync_SilentServer_CancelsWithinTimeout()
    {
        _camera.Mode = LoopbackViscaCameraMode.Silent;
        var transport = new ViscaTcpTransport();
        await transport.ConnectAsync("127.0.0.1", _camera.Port);
        await transport.SendAsync(ViscaCommandEncoder.AutoFocus());

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        var stopwatch = Stopwatch.StartNew();

        await Assert.ThrowsAnyAsync<Exception>(() => transport.ReceiveFrameAsync(cts.Token));

        stopwatch.Stop();
        Assert.True(stopwatch.ElapsedMilliseconds < 1500, $"Expected cancellation to unblock the pending read quickly, took {stopwatch.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task DisconnectAsync_ThenReconnect_Succeeds()
    {
        var transport = new ViscaTcpTransport();
        await transport.ConnectAsync("127.0.0.1", _camera.Port);
        await transport.DisconnectAsync();

        await transport.ConnectAsync("127.0.0.1", _camera.Port);

        Assert.True(transport.IsConnected);
        await transport.DisconnectAsync();
    }
}
