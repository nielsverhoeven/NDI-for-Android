using Moq;
using NdiForAndroid.Features.Settings.Services;
using NdiForAndroid.Features.Sources.Models;
using NdiForAndroid.Features.Sources.Repositories;
using NdiForAndroid.Features.Sources.ViewModels;
using NdiForAndroid.NdiBridge;
using NdiForAndroid.Services;
using Xunit;

namespace MauiApp.Tests.Features.Sources;

public class SourceListViewModelTests
{
    private readonly Mock<ISourceRepository> _repositoryMock = new();
    private readonly Mock<INavigationService> _navigationMock = new();
    private readonly Mock<IDiscoverySettingsOrchestrator> _orchestratorMock = new();

    private SourceListViewModel CreateSut(DiscoveryMode mode = DiscoveryMode.Mdns)
    {
        _orchestratorMock.Setup(o => o.ActiveMode).Returns(mode);
        return new(_repositoryMock.Object, _navigationMock.Object, _orchestratorMock.Object);
    }

    [Fact]
    public async Task RefreshCommand_OnSuccess_PopulatesSources()
    {
        var sources = new List<NdiSource>
        {
            new("src-1", "Camera 1", "192.168.1.10", true, 1000),
            new("src-2", "Camera 2", "192.168.1.11", true, 2000),
        };
        _repositoryMock.Setup(r => r.DiscoverAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscoverySnapshot("snap-1", DiscoveryStatus.Success, sources,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));

        var sut = CreateSut();
        await sut.RefreshCommand.ExecuteAsync(null);

        Assert.Equal(2, sut.Sources.Count);
        Assert.Equal("Camera 1", sut.Sources[0].DisplayName);
        Assert.Null(sut.ErrorMessage);
        Assert.False(sut.IsRefreshing);
    }

    [Fact]
    public async Task RefreshCommand_OnFailure_SetsErrorMessage()
    {
        _repositoryMock.Setup(r => r.DiscoverAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscoverySnapshot("snap-1", DiscoveryStatus.Failure, Array.Empty<NdiSource>(),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), "Connection refused"));

        var sut = CreateSut();
        await sut.RefreshCommand.ExecuteAsync(null);

        Assert.Empty(sut.Sources);
        Assert.Equal("Connection refused", sut.ErrorMessage);
        Assert.False(sut.IsRefreshing);
    }

    [Fact]
    public async Task NavigateToViewerCommand_CallsNavigationService_WithCorrectRoute()
    {
        var source = new NdiSource("src-abc", "Camera 1", "192.168.1.10", true, 1000);
        var sut = CreateSut();

        await sut.NavigateToViewerCommand.ExecuteAsync(source);

        var expectedRoute = $"viewer?sourceId={Uri.EscapeDataString("src-abc")}";
        _navigationMock.Verify(
            n => n.NavigateToAsync(expectedRoute),
            Times.Once,
            $"Expected NavigateToAsync to be called with ''{expectedRoute}''");
    }

    [Fact]
    public async Task NavigateToViewerCommand_WithNullOrEmptySourceId_StillCallsNavigation()
    {
        var source = new NdiSource(string.Empty, "Unknown", "0.0.0.0", false, 0);
        var sut = CreateSut();

        await sut.NavigateToViewerCommand.ExecuteAsync(source);

        var expectedRoute = $"viewer?sourceId={Uri.EscapeDataString(string.Empty)}";
        _navigationMock.Verify(n => n.NavigateToAsync(expectedRoute), Times.Once);
    }

    [Fact]
    public async Task RefreshCommand_InMdnsMode_SetsActiveDiscoveryModeLabelToMdns()
    {
        _repositoryMock.Setup(r => r.DiscoverAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscoverySnapshot("snap-1", DiscoveryStatus.Success, Array.Empty<NdiSource>(),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));

        var sut = CreateSut(DiscoveryMode.Mdns);
        await sut.RefreshCommand.ExecuteAsync(null);

        Assert.Equal("mDNS", sut.ActiveDiscoveryModeLabel);
    }

    [Fact]
    public async Task RefreshCommand_InDiscoveryServerMode_SetsActiveDiscoveryModeLabelContainingDiscoveryServer()
    {
        _repositoryMock.Setup(r => r.DiscoverAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DiscoverySnapshot("snap-1", DiscoveryStatus.Success, Array.Empty<NdiSource>(),
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));

        var sut = CreateSut(DiscoveryMode.DiscoveryServer);
        await sut.RefreshCommand.ExecuteAsync(null);

        Assert.Contains("Discovery Server", sut.ActiveDiscoveryModeLabel);
    }

    [Fact]
    public void StopDiscoveryCommand_CanBeInvokedWithoutError()
    {
        var sut = CreateSut();
        // Should complete without throwing.
        sut.StopDiscoveryCommand.Execute(null);
    }

    [Fact]
    public void ViewModel_DoesNotHaveNavigateToOutputCommand()
    {
        var sut = CreateSut();
        var type = sut.GetType();
        var prop = type.GetProperty("NavigateToOutputCommand");
        Assert.Null(prop);
    }
}
