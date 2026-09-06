using System.Diagnostics;
using NdiForAndroid.Features.Ptz.Models;
using NdiForAndroid.Features.Ptz.Services;
using NdiForAndroid.Tests.Features.Ptz.Fakes;
using Xunit;

namespace NdiForAndroid.Tests.Features.Ptz;

public class ViscaPtzControllerLoopbackTests : IAsyncLifetime
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(300);

    // Deliberately larger than ViscaPtzController's production defaults (3 s connect / 2 s command): these tests do not test timing.
    private static readonly TimeSpan GenerousTimeout = TimeSpan.FromSeconds(5);

    private readonly LoopbackViscaCamera _camera = new();

    public Task InitializeAsync() => _camera.InitializeAsync();

    public Task DisposeAsync() => _camera.DisposeAsync();

    private ViscaPtzController CreateSut(TimeSpan? connectTimeout = null, TimeSpan? commandTimeout = null) =>
        new(
            new ViscaTcpTransport(),
            new PtzEndpoint("127.0.0.1", _camera.Port),
            connectTimeout ?? GenerousTimeout,
            commandTimeout ?? GenerousTimeout);

    [Fact]
    public async Task PanTiltAsync_RespondMode_ReturnsTrue()
    {
        var sut = CreateSut();

        var result = await sut.PanTiltAsync(1f, 0f);

        Assert.True(result);
        Assert.Equal(PtzLinkState.Connected, sut.LinkState);
        await sut.ShutdownAsync();
    }

    [Fact]
    public async Task PanTiltAsync_ErrorMode_ReturnsFalseButStaysConnected()
    {
        _camera.Mode = LoopbackViscaCameraMode.Error;
        var sut = CreateSut();

        var result = await sut.PanTiltAsync(1f, 0f);

        Assert.False(result);
        Assert.Equal(PtzLinkState.Connected, sut.LinkState);
        await sut.ShutdownAsync();
    }

    [Fact]
    public async Task PanTiltAsync_SilentMode_TimesOutWithoutHanging()
    {
        _camera.Mode = LoopbackViscaCameraMode.Silent;
        var sut = CreateSut(GenerousTimeout, ShortTimeout);
        var stopwatch = Stopwatch.StartNew();

        var result = await sut.PanTiltAsync(1f, 0f);

        stopwatch.Stop();
        Assert.False(result);
        Assert.Equal(PtzLinkState.Error, sut.LinkState);
        Assert.True(stopwatch.ElapsedMilliseconds < 1500, $"Expected the command timeout to fire quickly, took {stopwatch.ElapsedMilliseconds}ms");
        await sut.ShutdownAsync();
    }

    [Fact]
    public async Task PanTiltAsync_DropAfterFirstCommand_RetriesOnSecondCallAndSucceeds()
    {
        _camera.Mode = LoopbackViscaCameraMode.DropAfterFirstCommand;
        var sut = CreateSut();

        var first = await sut.PanTiltAsync(1f, 0f);
        var second = await sut.PanTiltAsync(0f, 1f);

        Assert.True(first);
        Assert.True(second);
        Assert.Equal(PtzLinkState.Connected, sut.LinkState);
        await sut.ShutdownAsync();
    }

    [Fact]
    public async Task ShutdownAsync_AfterCommand_Disconnects()
    {
        var sut = CreateSut();
        await sut.PanTiltAsync(1f, 0f);

        await sut.ShutdownAsync();

        Assert.Equal(PtzLinkState.Disconnected, sut.LinkState);
    }
}
