using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiForAndroid.Features.Settings.Models;
using NdiForAndroid.Features.Settings.Repositories;
using NdiForAndroid.Features.Settings.Services;
using NdiForAndroid.Features.Sources.Models;
using NdiForAndroid.Features.Sources.Repositories;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace NdiForAndroid.Features.Settings.ViewModels;

public enum SettingsSection
{
    General,
    Appearance,
    Discovery,
    DeveloperTools,
    About,
}

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsRepository _repository;
    private readonly ISettingsValidationService _validationService;
    private readonly ISettingsPlatformService _platformService;
    private readonly ISourceRepository _sourceRepository;

    private NdiSettingsSnapshot _baselineSnapshot = NdiSettingsSnapshot.CreateDefault();
    private DiscoveryServerItem? _editingDiscoveryServer;
    private bool _suppressStateTracking;

    private const string ThemeSystemLabel = "System default";

    public string GeneralGuidanceText => "Adjust settings by category, then select Apply to save all staged changes.";

    public IReadOnlyList<string> ThemeOptions { get; } = ["Light", "Dark", ThemeSystemLabel];

    public IReadOnlyList<string> AccentColorOptions { get; } = ["Blue", "Teal", "Green", "Orange", "Red", "Pink"];

    [ObservableProperty]
    private SettingsSection _selectedSection = SettingsSection.General;

    [ObservableProperty]
    private string? _discoveryHost;

    [ObservableProperty]
    private string? _discoveryPort;

    [ObservableProperty]
    private bool _developerModeEnabled;

    [ObservableProperty]
    private string _selectedThemeOption = ThemeSystemLabel;

    [ObservableProperty]
    private string _selectedAccentColor = AccentColorOption.Blue.ToString();

    [ObservableProperty]
    private string _discoveryServerEndpointInput = string.Empty;

    private const int DefaultDiscoveryServerPort = 5959;

    [ObservableProperty]
    private string _discoveryServerActionText = "Add Server";

    [ObservableProperty]
    private string _discoveryServersValidationMessage = string.Empty;

    [ObservableProperty]
    private string _generalValidationMessage = string.Empty;

    [ObservableProperty]
    private string? _validationError;

    [ObservableProperty]
    private bool _isApplied;

    [ObservableProperty]
    private bool _hasPendingChanges;

    [ObservableProperty]
    private string _appName = string.Empty;

    [ObservableProperty]
    private string _appVersionBuild = string.Empty;

    public ObservableCollection<DiscoveryServerItem> DiscoveryServers { get; } = [];

    public ObservableCollection<CachedSourceRegistryEntry> CachedSourceRegistry { get; } = [];

    public bool IsGeneralSectionSelected => SelectedSection == SettingsSection.General;
    public bool IsAppearanceSectionSelected => SelectedSection == SettingsSection.Appearance;
    public bool IsDiscoverySectionSelected => SelectedSection == SettingsSection.Discovery;
    public bool IsDeveloperToolsSectionSelected => SelectedSection == SettingsSection.DeveloperTools;
    public bool IsAboutSectionSelected => SelectedSection == SettingsSection.About;

    public SettingsViewModel(
        ISettingsRepository repository,
        ISettingsValidationService validationService,
        ISettingsPlatformService platformService,
        ISourceRepository sourceRepository)
    {
        _repository = repository;
        _validationService = validationService;
        _platformService = platformService;
        _sourceRepository = sourceRepository;

        var info = _platformService.GetAppInfo();
        AppName = info.AppName;
        AppVersionBuild = $"{info.Version} ({info.Build})";

        DiscoveryServers.CollectionChanged += OnDiscoveryServersCollectionChanged;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        var settings = await _repository.GetSettingsAsync();
        var cachedSources = await SafeGetCachedSourcesAsync();

        _suppressStateTracking = true;

        DiscoveryHost = settings.DiscoveryHost;
        DiscoveryPort = settings.DiscoveryPort?.ToString();
        DeveloperModeEnabled = settings.DeveloperModeEnabled;
        SelectedThemeOption = ToThemeOption(settings.ThemeMode);
        SelectedAccentColor = settings.AccentColor.ToString();

        DiscoveryServers.Clear();
        foreach (var server in settings.DiscoveryServers.OrderBy(server => server.Order))
            DiscoveryServers.Add(new DiscoveryServerItem(server.Host, server.Port.ToString(), server.Enabled));

        CachedSourceRegistry.Clear();
        foreach (var source in cachedSources)
        {
            CachedSourceRegistry.Add(new CachedSourceRegistryEntry(
                source.DisplayName,
                string.IsNullOrWhiteSpace(source.EndpointAddress) ? "-" : source.EndpointAddress,
                source.IsAvailable ? "Available" : "Unavailable",
                source.SourceId,
                FormatLastSeen(source.LastSeenAtEpochMillis)));
        }

        _baselineSnapshot = settings;
        _editingDiscoveryServer = null;
        DiscoveryServerActionText = "Add Server";

        GeneralValidationMessage = string.Empty;
        DiscoveryServersValidationMessage = string.Empty;
        ValidationError = null;
        IsApplied = false;
        HasPendingChanges = false;

        _suppressStateTracking = false;
        ApplyCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void SelectSection(SettingsSection section)
    {
        SelectedSection = section;
    }

    [RelayCommand(CanExecute = nameof(CanApply))]
    private async Task ApplyAsync()
    {
        IsApplied = false;
        ValidationError = null;
        GeneralValidationMessage = string.Empty;
        DiscoveryServersValidationMessage = string.Empty;

        if (!TryBuildValidatedSnapshot(out var snapshot, out var error))
        {
            ValidationError = error ?? "The settings are invalid.";
            if (!string.IsNullOrWhiteSpace(ValidationError) && ValidationError.Contains("Discovery server", StringComparison.OrdinalIgnoreCase))
                DiscoveryServersValidationMessage = ValidationError;
            else
                GeneralValidationMessage = ValidationError ?? string.Empty;

            ApplyCommand.NotifyCanExecuteChanged();
            return;
        }

        await _repository.SaveSettingsAsync(snapshot);

        _baselineSnapshot = snapshot;
        _editingDiscoveryServer = null;
        DiscoveryServerActionText = "Add Server";
        HasPendingChanges = false;
        IsApplied = true;
        ApplyCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void AddOrUpdateDiscoveryServer()
    {
        IsApplied = false;

        var endpoint = DiscoveryServerEndpointInput.Trim();
        var host = NormalizeHost(DiscoveryServerEndpointInput);
        var port = DefaultDiscoveryServerPort;

        // Parse optional port from "host:port" format
        if (endpoint.Contains(':'))
        {
            var lastColon = endpoint.LastIndexOf(':');
            var hostPart = endpoint[..lastColon].Trim();
            var portPart = endpoint[(lastColon + 1)..].Trim();

            if (!string.IsNullOrWhiteSpace(hostPart))
                host = NormalizeHost(hostPart);

            if (int.TryParse(portPart, out var parsedPort) && parsedPort is >= 1 and <= 65535)
                port = parsedPort;
        }

        if (!_validationService.IsValidHostOrEmpty(host) || string.IsNullOrWhiteSpace(host))
        {
            ValidationError = "Discovery server host must be a valid hostname or IP address.";
            DiscoveryServersValidationMessage = ValidationError;
            return;
        }

        var duplicateExists = DiscoveryServers.Any(item =>
            !ReferenceEquals(item, _editingDiscoveryServer) &&
            string.Equals(item.Host.Trim(), host, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(item.Port, out var existingPort) &&
            existingPort == port);

        if (duplicateExists)
        {
            ValidationError = "Discovery servers cannot contain duplicate host and port combinations.";
            DiscoveryServersValidationMessage = ValidationError;
            return;
        }

        if (_editingDiscoveryServer is null)
        {
            DiscoveryServers.Add(new DiscoveryServerItem(host, port.ToString(), true));
        }
        else
        {
            _editingDiscoveryServer.Host = host;
            _editingDiscoveryServer.Port = port.ToString();
            _editingDiscoveryServer = null;
            DiscoveryServerActionText = "Add Server";
        }

        DiscoveryServerEndpointInput = string.Empty;
        DiscoveryServersValidationMessage = string.Empty;
        ValidationError = null;

        RefreshPendingState();
    }

    [RelayCommand]
    private void EditDiscoveryServer(DiscoveryServerItem? item)
    {
        if (item is null)
            return;

        var port = string.IsNullOrWhiteSpace(item.Port) ? DefaultDiscoveryServerPort.ToString() : item.Port;
        DiscoveryServerEndpointInput = $"{item.Host}:{port}";
        _editingDiscoveryServer = item;
        DiscoveryServerActionText = "Update Server";
    }

    [RelayCommand]
    private void RemoveDiscoveryServer(DiscoveryServerItem? item)
    {
        if (item is null)
            return;

        if (ReferenceEquals(item, _editingDiscoveryServer))
        {
            _editingDiscoveryServer = null;
            DiscoveryServerEndpointInput = string.Empty;
            DiscoveryServerActionText = "Add Server";
        }

        DiscoveryServers.Remove(item);
        IsApplied = false;
        RefreshPendingState();
    }

    [RelayCommand]
    private void MoveDiscoveryServerUp(DiscoveryServerItem? item)
    {
        if (item is null)
            return;

        var index = DiscoveryServers.IndexOf(item);
        if (index <= 0)
            return;

        DiscoveryServers.Move(index, index - 1);
        IsApplied = false;
        RefreshPendingState();
    }

    [RelayCommand]
    private void MoveDiscoveryServerDown(DiscoveryServerItem? item)
    {
        if (item is null)
            return;

        var index = DiscoveryServers.IndexOf(item);
        if (index < 0 || index >= DiscoveryServers.Count - 1)
            return;

        DiscoveryServers.Move(index, index + 1);
        IsApplied = false;
        RefreshPendingState();
    }

    partial void OnSelectedSectionChanged(SettingsSection value)
    {
        _ = value;
        OnPropertyChanged(nameof(IsGeneralSectionSelected));
        OnPropertyChanged(nameof(IsAppearanceSectionSelected));
        OnPropertyChanged(nameof(IsDiscoverySectionSelected));
        OnPropertyChanged(nameof(IsDeveloperToolsSectionSelected));
        OnPropertyChanged(nameof(IsAboutSectionSelected));
    }

    partial void OnDiscoveryHostChanged(string? value)
    {
        _ = value;
        IsApplied = false;
        RefreshPendingState();
    }

    partial void OnDiscoveryPortChanged(string? value)
    {
        _ = value;
        IsApplied = false;
        RefreshPendingState();
    }

    partial void OnDeveloperModeEnabledChanged(bool value)
    {
        _ = value;
        IsApplied = false;
        RefreshPendingState();
    }

    partial void OnSelectedThemeOptionChanged(string value)
    {
        _ = value;
        IsApplied = false;
        RefreshPendingState();
    }

    partial void OnSelectedAccentColorChanged(string value)
    {
        _ = value;
        IsApplied = false;
        RefreshPendingState();
    }

    private bool CanApply()
    {
        if (!HasPendingChanges)
            return false;

        return TryBuildValidatedSnapshot(out _, out _);
    }

    private bool TryBuildValidatedSnapshot(out NdiSettingsSnapshot snapshot, out string? error)
    {
        snapshot = NdiSettingsSnapshot.CreateDefault();
        error = null;

        var host = NormalizeHost(DiscoveryHost);
        var port = ParseOptionalPort(DiscoveryPort);

        if (port is null && !string.IsNullOrWhiteSpace(DiscoveryPort))
        {
            error = "Port must be a number between 1 and 65535.";
            return false;
        }

        if (!_validationService.IsValidHostOrEmpty(host))
        {
            error = "Discovery host must be empty or a valid hostname or IP address.";
            return false;
        }

        var selectedTheme = ParseThemeOption(SelectedThemeOption);
        var selectedAccent = ParseAccentColorOption(SelectedAccentColor);

        var discoveryServers = new List<DiscoveryServerPreference>(DiscoveryServers.Count);
        for (var index = 0; index < DiscoveryServers.Count; index++)
        {
            var item = DiscoveryServers[index];
            if (string.IsNullOrWhiteSpace(item.Host) && string.IsNullOrWhiteSpace(item.Port))
                continue;

            if (!_validationService.IsValidHostOrEmpty(item.Host) || string.IsNullOrWhiteSpace(item.Host))
            {
                error = "Each discovery server must provide a valid hostname or IP address.";
                return false;
            }

            if (!int.TryParse(item.Port, out var serverPort) || serverPort is < 1 or > 65535)
            {
                error = "Each discovery server port must be a number between 1 and 65535.";
                return false;
            }

            discoveryServers.Add(new DiscoveryServerPreference(item.Host.Trim(), serverPort, item.Enabled, index));
        }

        var staged = new NdiSettingsSnapshot(
            host,
            port,
            DeveloperModeEnabled,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            selectedTheme,
            selectedAccent,
            discoveryServers);

        snapshot = _validationService.Sanitize(staged);

        if (!_validationService.TryValidateForSave(snapshot, out error))
            return false;

        return true;
    }

    private async Task<IReadOnlyList<NdiSource>> SafeGetCachedSourcesAsync()
    {
        try
        {
            return await _sourceRepository.GetCachedSourcesAsync();
        }
        catch
        {
            return Array.Empty<NdiSource>();
        }
    }

    private static string FormatLastSeen(long epochMillis)
    {
        if (epochMillis <= 0)
            return "Never";

        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(epochMillis)
                .ToUniversalTime()
                .ToString("yyyy-MM-dd HH:mm:ss 'UTC'");
        }
        catch
        {
            return "Never";
        }
    }

    private void OnDiscoveryServersCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _ = sender;

        if (e.OldItems is not null)
        {
            foreach (var oldItem in e.OldItems.OfType<DiscoveryServerItem>())
                oldItem.PropertyChanged -= OnDiscoveryServerItemPropertyChanged;
        }

        if (e.NewItems is not null)
        {
            foreach (var newItem in e.NewItems.OfType<DiscoveryServerItem>())
                newItem.PropertyChanged += OnDiscoveryServerItemPropertyChanged;
        }

        IsApplied = false;
        RefreshPendingState();
    }

    private void OnDiscoveryServerItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        _ = sender;
        _ = e;

        IsApplied = false;
        RefreshPendingState();
    }

    private void RefreshPendingState()
    {
        if (_suppressStateTracking)
            return;

        HasPendingChanges = HasStateChangedComparedToBaseline();
        ApplyCommand.NotifyCanExecuteChanged();
    }

    private bool HasStateChangedComparedToBaseline()
    {
        if (NormalizeHost(DiscoveryHost) != _baselineSnapshot.DiscoveryHost)
            return true;

        if (ParseOptionalPort(DiscoveryPort) != _baselineSnapshot.DiscoveryPort)
            return true;

        if (DeveloperModeEnabled != _baselineSnapshot.DeveloperModeEnabled)
            return true;

        if (ParseThemeOption(SelectedThemeOption) != _baselineSnapshot.ThemeMode)
            return true;

        if (ParseAccentColorOption(SelectedAccentColor) != _baselineSnapshot.AccentColor)
            return true;

        if (DiscoveryServers.Count != _baselineSnapshot.DiscoveryServers.Count)
            return true;

        for (var index = 0; index < DiscoveryServers.Count; index++)
        {
            var current = DiscoveryServers[index];
            var baseline = _baselineSnapshot.DiscoveryServers[index];

            if (!string.Equals(current.Host.Trim(), baseline.Host, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!int.TryParse(current.Port, out var parsedPort) || parsedPort != baseline.Port)
                return true;

            if (current.Enabled != baseline.Enabled)
                return true;
        }

        return false;
    }

    private static ThemeMode ParseThemeOption(string? option)
    {
        if (string.Equals(option, "Light", StringComparison.OrdinalIgnoreCase))
            return ThemeMode.Light;

        if (string.Equals(option, "Dark", StringComparison.OrdinalIgnoreCase))
            return ThemeMode.Dark;

        return ThemeMode.System;
    }

    private static string ToThemeOption(ThemeMode mode)
        => mode switch
        {
            ThemeMode.Light => "Light",
            ThemeMode.Dark => "Dark",
            _ => ThemeSystemLabel,
        };

    private static AccentColorOption ParseAccentColorOption(string? option)
    {
        if (Enum.TryParse<AccentColorOption>(option, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed))
            return parsed;

        return AccentColorOption.Blue;
    }

    private static string? NormalizeHost(string? host)
        => string.IsNullOrWhiteSpace(host) ? null : host.Trim();

    private static int? ParseOptionalPort(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!int.TryParse(value, out var parsed) || parsed is < 1 or > 65535)
            return null;

        return parsed;
    }
}
