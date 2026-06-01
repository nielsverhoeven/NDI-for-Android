using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiForAndroid.Features.Settings.Models;
using NdiForAndroid.Features.Settings.Repositories;

namespace NdiForAndroid.Features.Settings.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsRepository _repository;

    [ObservableProperty]
    private string? _discoveryHost;

    [ObservableProperty]
    private string? _discoveryPort;

    [ObservableProperty]
    private bool _developerModeEnabled;

    [ObservableProperty]
    private string? _validationError;

    [ObservableProperty]
    private bool _isSaved;

    public SettingsViewModel(ISettingsRepository repository)
    {
        _repository = repository;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        var settings = await _repository.GetSettingsAsync();
        DiscoveryHost = settings.DiscoveryHost;
        DiscoveryPort = settings.DiscoveryPort?.ToString();
        DeveloperModeEnabled = settings.DeveloperModeEnabled;
        ValidationError = null;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        ValidationError = null;
        int? port = null;

        if (!string.IsNullOrWhiteSpace(DiscoveryPort))
        {
            if (!int.TryParse(DiscoveryPort, out var parsed) || parsed < 1 || parsed > 65535)
            {
                ValidationError = "Port must be a number between 1 and 65535.";
                return;
            }
            port = parsed;
        }

        var snapshot = new NdiSettingsSnapshot(
            DiscoveryHost?.Trim(),
            port,
            DeveloperModeEnabled,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        await _repository.SaveSettingsAsync(snapshot);
        IsSaved = true;
    }
}
