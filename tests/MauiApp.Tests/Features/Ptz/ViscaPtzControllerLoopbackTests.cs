using System.Diagnostics;
using Moq;
using NdiForAndroid.Features.Ptz.Models;
using NdiForAndroid.Features.Ptz.Services;
using NdiForAndroid.Features.Ptz.ViewModels;
using NdiForAndroid.NdiBridge;
using NdiForAndroid.Tests.Features.Ptz.Fakes;
using Xunit;

namespace NdiForAndroid.Tests.Features.Ptz;

public class ViscaPtzControllerLoopbackTests : IAsyncLifetime
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan SilentDetectionTimeout = TimeSpan.FromSeconds(1);

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

    [Fact]
    public async Task PanTiltAsync_AckAndCompletionCamera_ManyCommands_AllReturnTrue()
    {
        _camera.Mode = LoopbackViscaCameraMode.AckAndCompletion;
        var sut = CreateSut();

        for (var i = 1; i <= 20; i++)
            Assert.True(await sut.PanTiltAsync(1f, 0f), $"command {i} should succeed");

        Assert.Equal(20, _camera.ReceivedCommandCount);
        await sut.ShutdownAsync();
    }

    [Fact]
    public async Task PanTiltAsync_AckAndCompletionCamera_ThenSilent_TimesOutInsteadOfReadingAStaleCompletion()
    {
        _camera.Mode = LoopbackViscaCameraMode.AckAndCompletion;
        var sut = CreateSut(GenerousTimeout, SilentDetectionTimeout);
        for (var i = 1; i <= 20; i++)
            Assert.True(await sut.PanTiltAsync(1f, 0f), $"command {i} should succeed");

        _camera.Mode = LoopbackViscaCameraMode.Silent;
        var stopwatch = Stopwatch.StartNew();
        var result = await sut.PanTiltAsync(1f, 0f);
        stopwatch.Stop();

        Assert.False(result);
        Assert.Equal(PtzLinkState.Error, sut.LinkState);
        Assert.Equal("Camera did not answer (timed out).", sut.LastError);
        Assert.InRange(stopwatch.ElapsedMilliseconds, 900, 3999);
        await sut.ShutdownAsync();
    }

    [Fact]
    public async Task PanTiltAsync_ErrorReplyOnAnOpenConnection_IsNotResent()
    {
        _camera.Mode = LoopbackViscaCameraMode.Respond;
        var sut = CreateSut();
        Assert.True(await sut.PanTiltAsync(1f, 0f));

        _camera.Mode = LoopbackViscaCameraMode.Error;
        var second = await sut.PanTiltAsync(0f, 1f);

        Assert.False(second);
        Assert.Equal(2, _camera.ReceivedCommandCount);
        await sut.ShutdownAsync();
    }

    [Fact]
    public async Task PanTiltAsync_CompletionOnlyCamera_ReturnsTrue()
    {
        _camera.Mode = LoopbackViscaCameraMode.CompletionOnly;
        var sut = CreateSut();

        for (var i = 1; i <= 5; i++)
            Assert.True(await sut.PanTiltAsync(1f, 0f), $"command {i} should succeed");

        await sut.ShutdownAsync();
    }

    [Fact]
    public async Task PanTiltAsync_AckThenSocketErrorOnEarlierCommand_ThenSilent_TimesOut()
    {
        _camera.Mode = LoopbackViscaCameraMode.AckThenSocketError;
        var sut = CreateSut(GenerousTimeout, SilentDetectionTimeout);
        Assert.True(await sut.PanTiltAsync(1f, 0f));

        _camera.Mode = LoopbackViscaCameraMode.Silent;
        var second = await sut.PanTiltAsync(0f, 1f);

        Assert.False(second);
        Assert.Equal(PtzLinkState.Error, sut.LinkState);
        Assert.Equal("Camera did not answer (timed out).", sut.LastError);
        await sut.ShutdownAsync();
    }

    [Fact]
    public async Task PanTiltAsync_ErrorOnNthCommand_IsReportedForThatCommandAndNotResent()
    {
        _camera.Mode = LoopbackViscaCameraMode.AckAndCompletion;
        _camera.RejectCommandNumber = 3;
        var sut = CreateSut();
        var results = new List<bool>();
        string? errorAfterThird = null;

        for (var i = 1; i <= 6; i++)
        {
            results.Add(await sut.PanTiltAsync(1f, 0f));
            if (i == 3)
                errorAfterThird = sut.LastError;
        }

        Assert.Equal(new[] { true, true, false, true, true, true }, results);
        Assert.Equal("VISCA error 0x41", errorAfterThird);
        Assert.Equal(6, _camera.ReceivedCommandCount);
        await sut.ShutdownAsync();
    }

    [Fact]
    public async Task EndpointFormTest_SilentCamera_ShowsTheMappedTimeoutMessage()
    {
        _camera.Mode = LoopbackViscaCameraMode.Silent;
        var factory = new PtzControllerFactory(new Mock<INdiViewerBridge>().Object, new ViscaTransportFactory());
        var sut = new PtzEndpointFormViewModel(factory);
        sut.Open("127.0.0.1", _camera.Port);

        await sut.TestCommand.ExecuteAsync(null);

        Assert.Equal("Camera did not answer (timed out).", sut.StatusText);
    }
}
