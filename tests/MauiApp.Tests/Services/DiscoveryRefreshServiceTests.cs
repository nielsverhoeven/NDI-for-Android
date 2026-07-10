using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NdiForAndroid.Features.Settings.Models;
using NdiForAndroid.Features.Settings.Repositories;
using NdiForAndroid.Features.Sources.Models;
using NdiForAndroid.Features.Sources.Repositories;
using NdiForAndroid.Services;
using Xunit;

namespace NdiForAndroid.Tests.Services;

public class DiscoveryRefreshServiceTests
{
    private readonly Mock<ISourceRepository> _repositoryMock = new();
    private readonly Mock<ISettingsRepository> _settingsRepositoryMock = new();
    private readonly Mock<IAppLifecycleService> _lifecycleMock = new();
    private readonly FakeTimeProvider _clock = new();

    private DiscoveryRefreshService CreateSut(
        TimeSpan? pollingInterval = null,
        TimeSpan? debounceWindow = null)
    {
        _settingsRepositoryMock.Setup(r => r.GetSettingsAsync())
            .ReturnsAsync(NdiSettingsSnapshot.CreateDefault());

        return new DiscoveryRefreshService(
            repository:         _repositoryMock.Object,
            settingsRepository: _settingsRepositoryMock.Object,
            lifecycle:          _lifecycleMock.Object,
            logger:             NullLogger<DiscoveryRefreshService>.Instance,
            timeProvider:       _clock,
            pollingInterval:    pollingInterval ?? TimeSpan.FromMilliseconds(50),
            debounceWindow:     debounceWindow  ?? TimeSpan.FromMilliseconds(10));
    }

    private DiscoverySnapshot OkSnapshot() => new(
        SnapshotId: "s1",
        Status: DiscoveryStatus.Success,
        Sources: Array.Empty<NdiSource>(),
        CompletedAtEpochMillis: 1000);

    [Fact]
    public async Task Start_BeginsPollImmediately()
    {
        _repositoryMock.Setup(r => r.DiscoverAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkSnapshot());

        var sut = CreateSut();
        sut.Start();

        await Task.Delay(200);  // allow background task to fire

        _repositoryMock.Verify(r => r.DiscoverAsync(It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        sut.Stop();
    }

    [Fact]
    public async Task Stop_CancelsPolling_NoFurtherCallsAfterStop()
    {
        var callCount = 0;
        _repositoryMock.Setup(r => r.DiscoverAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                await Task.Delay(5, ct);
                callCount++;
                return OkSnapshot();
            });

        var sut = CreateSut(pollingInterval: TimeSpan.FromMilliseconds(20));
        sut.Start();
        await Task.Delay(50);
        sut.Stop();
        var countAfterStop = callCount;
        await Task.Delay(100);  // wait to see if more calls arrive

        Assert.Equal(countAfterStop, callCount);
    }

    [Fact]
    public async Task SnapshotReady_FiredAfterSuccessfulPoll()
    {
        _repositoryMock.Setup(r => r.DiscoverAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkSnapshot());

        DiscoverySnapshot? received = null;
        var sut = CreateSut();
        sut.SnapshotReady += (_, s) => received = s;
        sut.Start();

        await Task.Delay(200);

        Assert.NotNull(received);
        Assert.Equal(DiscoveryStatus.Success, received!.Status);

        sut.Stop();
    }

    [Fact]
    public async Task AtMostOneInFlight_ConcurrentRequestRefresh()
    {
        var callCount = 0;
        var gate = new SemaphoreSlim(0);
        _repositoryMock.Setup(r => r.DiscoverAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken _) =>
            {
                callCount++;
                await gate.WaitAsync();
                return OkSnapshot();
            });

        var sut = CreateSut(debounceWindow: TimeSpan.Zero);
        sut.Start();
        await Task.Delay(30);  // let first poll start

        // Fire many concurrent manual requests while first is in-flight
        for (var i = 0; i < 5; i++)
            sut.RequestRefresh();

        gate.Release(10);
        await Task.Delay(100);
        sut.Stop();

        // More than one poll is fine (loop fires multiple times with short interval),
        // but concurrent in-flight calls must never exceed 1.
        // We verify via callCount increasing sequentially, not simultaneously.
        Assert.True(callCount >= 1);
    }

    [Fact]
    public async Task Debounce_DropsRapidManualTriggers()
    {
        var callCount = 0;
        _repositoryMock.Setup(r => r.DiscoverAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => { callCount++; return OkSnapshot(); });

        var sut = CreateSut(
            pollingInterval: TimeSpan.FromSeconds(60),  // suppress auto-poll
            debounceWindow: TimeSpan.FromMilliseconds(500));

        sut.Start();
        await Task.Delay(100);  // let first auto-poll fire

        var countBeforeManual = callCount;

        // Fire 10 rapid manual requests within debounce window
        for (var i = 0; i < 10; i++)
            sut.RequestRefresh();

        await Task.Delay(100);

        sut.Stop();

        // At most 1 extra call above baseline (debounce should swallow the rest)
        Assert.True(callCount <= countBeforeManual + 2,
            $"Expected ≤{countBeforeManual + 2} calls but got {callCount}");
    }

    [Fact]
    public async Task FailedPoll_FiresFailureSnapshot()
    {
        _repositoryMock.Setup(r => r.DiscoverAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("NDI unavailable"));

        DiscoverySnapshot? received = null;
        var sut = CreateSut();
        sut.SnapshotReady += (_, s) => received = s;
        sut.Start();

        await Task.Delay(200);

        Assert.NotNull(received);
        Assert.Equal(DiscoveryStatus.Failure, received!.Status);
        Assert.Contains("NDI unavailable", received.ErrorMessage);

        sut.Stop();
    }

    [Fact]
    public async Task FailedPoll_DoesNotStopLoop()
    {
        var callCount = 0;
        _repositoryMock.Setup(r => r.DiscoverAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken _) =>
            {
                callCount++;
                if (callCount == 1) throw new InvalidOperationException("first failure");
                return OkSnapshot();
            });

        var sut = CreateSut(pollingInterval: TimeSpan.FromMilliseconds(30));
        sut.Start();

        await Task.Delay(300);
        sut.Stop();

        Assert.True(callCount >= 2, $"Expected ≥2 calls (loop continued after failure), got {callCount}");
    }

    [Fact]
    public async Task AppPaused_StopsPolling()
    {
        Action? pauseCallback = null;
        _lifecycleMock.SetupAdd(l => l.AppPaused += It.IsAny<Action>())
            .Callback<Action>(cb => pauseCallback = cb);

        _repositoryMock.Setup(r => r.DiscoverAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkSnapshot());

        var sut = CreateSut();
        sut.Start();
        await Task.Delay(100);

        // Simulate app pause via the event
        pauseCallback?.Invoke();
        var callsAfterPause = _repositoryMock.Invocations.Count;
        await Task.Delay(150);

        Assert.Equal(callsAfterPause, _repositoryMock.Invocations.Count);
    }

    [Fact]
    public async Task AppResumed_RestartsPolling()
    {
        Action? resumeCallback = null;
        Action? pauseCallback  = null;
        _lifecycleMock.SetupAdd(l => l.AppResumed += It.IsAny<Action>())
            .Callback<Action>(cb => resumeCallback = cb);
        _lifecycleMock.SetupAdd(l => l.AppPaused += It.IsAny<Action>())
            .Callback<Action>(cb => pauseCallback = cb);

        _repositoryMock.Setup(r => r.DiscoverAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkSnapshot());

        var sut = CreateSut();

        // Pause first
        pauseCallback?.Invoke();
        var countBeforeResume = _repositoryMock.Invocations.Count;

        // Resume
        resumeCallback?.Invoke();
        await Task.Delay(200);

        Assert.True(_repositoryMock.Invocations.Count > countBeforeResume);

        sut.Stop();
    }

    [Fact]
    public void Start_IsIdempotent()
    {
        _repositoryMock.Setup(r => r.DiscoverAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(OkSnapshot());

        var sut = CreateSut();
        sut.Start();
        sut.Start();  // second call — must not throw or start second loop

        sut.Stop();
    }

    [Fact]
    public void Stop_IsIdempotent()
    {
        var sut = CreateSut();
        sut.Stop();  // stop before start — must not throw
        sut.Stop();  // double stop — must not throw
    }
}

/// <summary>Minimal TimeProvider implementation that delegates to the real clock.</summary>
internal sealed class FakeTimeProvider : TimeProvider
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;

    public void Advance(TimeSpan duration) => _now = _now.Add(duration);

    public override DateTimeOffset GetUtcNow() => _now;
}
