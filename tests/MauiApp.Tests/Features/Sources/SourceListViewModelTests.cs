using Moq;
using NdiForAndroid.Features.Sources.Models;
using NdiForAndroid.Features.Sources.Repositories;
using NdiForAndroid.Features.Sources.ViewModels;
using NdiForAndroid.Services;
using Xunit;

namespace MauiApp.Tests.Features.Sources;

public class SourceListViewModelTests
{
    private readonly Mock<ISourceRepository> _repositoryMock = new();
    private readonly Mock<INavigationService> _navigationMock = new();
    private SourceListViewModel CreateSut() => new(_repositoryMock.Object, _navigationMock.Object);

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
}
