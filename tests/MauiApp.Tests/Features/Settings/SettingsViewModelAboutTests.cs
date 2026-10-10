using Moq;
using NdiForAndroid.Features.Settings.Models;
using NdiForAndroid.Features.Settings.Repositories;
using NdiForAndroid.Features.Settings.Services;
using NdiForAndroid.Features.Settings.ViewModels;
using NdiForAndroid.Features.Sources.Repositories;
using NdiForAndroid.NdiBridge;
using NdiForAndroid.Services;
using Xunit;

namespace NdiForAndroid.Tests.Features.Settings;

/// <summary>
/// About panel (#457): the NDI trademark attribution and the privacy-policy link that Google Play
/// requires for a camera/microphone app. The strings are Core constants so the XAML, the Play
/// listing and the Appium suite all share one source of truth; the link opens through
/// <see cref="ISettingsPlatformService.OpenExternalUrlAsync"/> and never throws into the UI.
/// </summary>
public sealed class SettingsViewModelAboutTests
{
    private readonly Mock<ISettingsRepository> _repositoryMock = new();
    private readonly ISettingsValidationService _validationService = new SettingsValidationService();
    private readonly Mock<ISettingsPlatformService> _platformServiceMock = new();
    private readonly Mock<ISourceRepository> _sourceRepositoryMock = new();
    private readonly Mock<INdiDiscoveryBridge> _discoveryBridgeMock = new();
    private readonly FakeUserPromptService _userPromptService = new();

    private SettingsViewModel CreateSut()
    {
        _platformServiceMock
            .Setup(s => s.GetAppInfo())
            .Returns(new SettingsAppInfo("NDI for Android", "2.0.0", "42"));

        _sourceRepositoryMock
            .Setup(r => r.GetCachedSourcesAsync())
            .ReturnsAsync(Array.Empty<NdiForAndroid.Features.Sources.Models.NdiSource>());

        _repositoryMock
            .Setup(r => r.SaveSettingsAsync(It.IsAny<NdiSettingsSnapshot>()))
            .Returns(Task.CompletedTask);

        return new SettingsViewModel(
            _repositoryMock.Object,
            _validationService,
            _platformServiceMock.Object,
            _sourceRepositoryMock.Object,
            new Mock<INdiVersionInfo>().Object,
            _discoveryBridgeMock.Object,
            new FakeMainThreadDispatcher(),
            _userPromptService);
    }

    [Fact]
    public async Task OpenPrivacyPolicy_OpensThePublishedPolicyUrl()
    {
        _platformServiceMock
            .Setup(s => s.OpenExternalUrlAsync(It.IsAny<Uri>()))
            .Returns(Task.CompletedTask);
        var sut = CreateSut();

        await sut.OpenPrivacyPolicyCommand.ExecuteAsync(null);

        _platformServiceMock.Verify(
            s => s.OpenExternalUrlAsync(new Uri(SettingsViewModel.PrivacyPolicyUrl)),
            Times.Once);
    }

    [Fact]
    public async Task OpenPrivacyPolicy_WhenThePlatformThrows_DoesNotPropagate()
    {
        _platformServiceMock
            .Setup(s => s.OpenExternalUrlAsync(It.IsAny<Uri>()))
            .ThrowsAsync(new InvalidOperationException("no browser on this device"));
        var sut = CreateSut();

        // Completes normally: the failure is traced to the diagnostic overlay, never thrown into the UI thread.
        await sut.OpenPrivacyPolicyCommand.ExecuteAsync(null);

        _platformServiceMock.Verify(s => s.OpenExternalUrlAsync(It.IsAny<Uri>()), Times.Once);
    }

    [Fact]
    public void PrivacyPolicyUrl_IsHttpsOnTheProjectPagesSite()
    {
        var url = new Uri(SettingsViewModel.PrivacyPolicyUrl);

        Assert.Equal(Uri.UriSchemeHttps, url.Scheme);
        Assert.Equal("nielsverhoeven.github.io", url.Host);
        Assert.StartsWith("/NDI-for-Android/", url.AbsolutePath, StringComparison.Ordinal);
        // Trailing-slash permalink form — the canonical URL declared in docs/privacy-policy.md front matter.
        Assert.EndsWith("/", url.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public void NdiTrademarkNotice_NamesVizrtAndCarriesTheRegisteredMark()
    {
        Assert.Contains("NDI®", SettingsViewModel.NdiTrademarkNotice, StringComparison.Ordinal);
        Assert.Contains("Vizrt NDI AB", SettingsViewModel.NdiTrademarkNotice, StringComparison.Ordinal);
    }
}
