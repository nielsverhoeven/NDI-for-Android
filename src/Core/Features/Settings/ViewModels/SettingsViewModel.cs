using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NdiForAndroid.Features.Settings.Models;
using NdiForAndroid.Features.Settings.Repositories;
using NdiForAndroid.Features.Settings.Services;
using NdiForAndroid.Features.Sources.Models;
using NdiForAndroid.Features.Sources.Repositories;
using NdiForAndroid.NdiBridge;
using NdiForAndroid.Services;
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

/// <summary>
/// Auto-saving settings view model (#292): every change (theme, accent, developer mode,
/// discovery-server add/edit/delete/toggle/reorder) persists immediately via
/// <see cref="ISettingsRepository.SaveSettingsAsync"/> — there is no Apply/staging step.
/// A background loop TCP-probes each enabled discovery server every ~10 s and surfaces
/// the result as <see cref="DiscoveryServerItem.ConnectionState"/>.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsRepository _repository;
    private readonly ISettingsValidationService _validationService;
    private readonly ISettingsPlatformService _platformService;
    private readonly ISourceRepository _sourceRepository;
    private readonly INdiDiscoveryBridge _discoveryBridge;
    private readonly IMainThreadDispatcher _dispatcher;
    private readonly TimeProvider _timeProvider;

    private DiscoveryServerItem? _editingDiscoveryServer;
    private bool _suppressAutoSave;

    private CancellationTokenSource? _statusMonitorCts;
    private volatile IReadOnlyList<DiscoveryServerItem> _statusTargets = Array.Empty<DiscoveryServerItem>();
    private int _statusCheckInFlight;

    private const int DefaultDiscoveryServerPort = 5959;
    private static readonly TimeSpan StatusCheckInterval = TimeSpan.FromSeconds(10);

    private const string ThemeSystemLabel = "System default";

    public string GeneralGuidanceText => "Settings are saved automatically as you change them.";

    public IReadOnlyList<string> ThemeOptions { get; } = ["Light", "Dark", ThemeSystemLabel];

    public IReadOnlyList<string> AccentColorOptions { get; } = ["Blue", "Teal", "Green", "Orange", "Red", "Pink"];

    [ObservableProperty]
    private SettingsSection _selectedSection = SettingsSection.General;

    [ObservableProperty]
    private bool _developerModeEnabled;

    [ObservableProperty]
    private string _selectedThemeOption = ThemeSystemLabel;

    [ObservableProperty]
    private string _selectedAccentColor = AccentColorOption.Blue.ToString();

    // ── Add-server form ─────────────────────────────────────────────────────

    [ObservableProperty]
    private string _newServerDisplayName = string.Empty;

    [ObservableProperty]
    private string _newServerHost = string.Empty;

    [ObservableProperty]
    private string _newServerPort = string.Empty;

    [ObservableProperty]
    private string _discoveryServersValidationMessage = string.Empty;

    // ── Edit-server dialog ──────────────────────────────────────────────────

    [ObservableProperty]
    private bool _isEditServerDialogOpen;

    [ObservableProperty]
    private string _editServerDisplayName = string.Empty;

    [ObservableProperty]
    private string _editServerHost = string.Empty;

    [ObservableProperty]
    private string _editServerPort = string.Empty;

    [ObservableProperty]
    private string _editServerValidationMessage = string.Empty;

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
        ISourceRepository sourceRepository,
        INdiVersionInfo ndiVersionInfo,
        INdiDiscoveryBridge discoveryBridge,
        IMainThreadDispatcher dispatcher,
        TimeProvider? timeProvider = null)
    {
        _repository = repository;
        _validationService = validationService;
        _platformService = platformService;
        _sourceRepository = sourceRepository;
        _discoveryBridge = discoveryBridge;
        _dispatcher = dispatcher;
        _timeProvider = timeProvider ?? TimeProvider.System;

        var info = _platformService.GetAppInfo();
        AppName = info.AppName;
        AppVersionBuild = $"{info.Version} ({info.Build})";
        NdiSdkVersion = ndiVersionInfo.IsRuntimeAvailable
            ? ndiVersionInfo.NativeVersion ?? "NDI runtime available (version unknown)"
            : "NDI runtime unavailable on this device";

        DiscoveryServers.CollectionChanged += OnDiscoveryServersCollectionChanged;
    }

    /// <summary>Native NDI SDK version (or an unavailability message on non-ARM devices).</summary>
    public string NdiSdkVersion { get; }

    [RelayCommand]
    private async Task LoadAsync()
    {
        var settings = await _repository.GetSettingsAsync();
        var cachedSources = await SafeGetCachedSourcesAsync();

        _suppressAutoSave = true;

        DeveloperModeEnabled = settings.DeveloperModeEnabled;
        SelectedThemeOption = ToThemeOption(settings.ThemeMode);
        SelectedAccentColor = settings.AccentColor.ToString();

        DiscoveryServers.Clear();
        foreach (var server in settings.DiscoveryServers.OrderBy(server => server.Order))
            DiscoveryServers.Add(new DiscoveryServerItem(server.Host, server.Port.ToString(), server.Enabled, server.DisplayName));

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

        _editingDiscoveryServer = null;
        IsEditServerDialogOpen = false;
        DiscoveryServersValidationMessage = string.Empty;
        EditServerValidationMessage = string.Empty;

        _suppressAutoSave = false;

        TriggerStatusCheck();
    }

    [RelayCommand]
    private void SelectSection(SettingsSection section)
    {
        SelectedSection = section;
    }

    // ── Discovery server commands ───────────────────────────────────────────

    [RelayCommand]
    private async Task AddDiscoveryServerAsync()
    {
        if (!TryParseServerInput(NewServerHost, NewServerPort, excludeItem: null, out var host, out var port, out var error))
        {
            DiscoveryServersValidationMessage = error;
            return;
        }

        DiscoveryServersValidationMessage = string.Empty;
        DiscoveryServers.Add(new DiscoveryServerItem(host, port.ToString(), true, NewServerDisplayName));

        NewServerDisplayName = string.Empty;
        NewServerHost = string.Empty;
        NewServerPort = string.Empty;

        await PersistAsync();
        TriggerStatusCheck();
    }

    [RelayCommand]
    private void EditDiscoveryServer(DiscoveryServerItem? item)
    {
        if (item is null)
            return;

        _editingDiscoveryServer = item;
        EditServerDisplayName = item.DisplayName ?? string.Empty;
        EditServerHost = item.Host;
        EditServerPort = item.Port;
        EditServerValidationMessage = string.Empty;
        IsEditServerDialogOpen = true;
    }

    [RelayCommand]
    private async Task SaveEditedDiscoveryServerAsync()
    {
        var item = _editingDiscoveryServer;
        if (item is null)
        {
            IsEditServerDialogOpen = false;
            return;
        }

        if (!TryParseServerInput(EditServerHost, EditServerPort, excludeItem: item, out var host, out var port, out var error))
        {
            EditServerValidationMessage = error;
            return;
        }

        item.DisplayName = string.IsNullOrWhiteSpace(EditServerDisplayName) ? null : EditServerDisplayName.Trim();
        item.Host = host;
        item.Port = port.ToString();
        item.ConnectionState = DiscoveryServerConnectionState.Unknown;

        _editingDiscoveryServer = null;
        IsEditServerDialogOpen = false;
        EditServerValidationMessage = string.Empty;

        await PersistAsync();
        TriggerStatusCheck();
    }

    [RelayCommand]
    private void CancelEditDiscoveryServer()
    {
        _editingDiscoveryServer = null;
        IsEditServerDialogOpen = false;
        EditServerValidationMessage = string.Empty;
    }

    [RelayCommand]
    private async Task RemoveDiscoveryServerAsync(DiscoveryServerItem? item)
    {
        if (item is null)
            return;

        if (ReferenceEquals(item, _editingDiscoveryServer))
            CancelEditDiscoveryServer();

        DiscoveryServers.Remove(item);
        await PersistAsync();
    }

    [RelayCommand]
    private async Task MoveDiscoveryServerUpAsync(DiscoveryServerItem? item)
    {
        if (item is null)
            return;

        var index = DiscoveryServers.IndexOf(item);
        if (index <= 0)
            return;

        DiscoveryServers.Move(index, index - 1);
        await PersistAsync();
    }

    [RelayCommand]
    private async Task MoveDiscoveryServerDownAsync(DiscoveryServerItem? item)
    {
        if (item is null)
            return;

        var index = DiscoveryServers.IndexOf(item);
        if (index < 0 || index >= DiscoveryServers.Count - 1)
            return;

        DiscoveryServers.Move(index, index + 1);
        await PersistAsync();
    }

    // ── Connection status monitoring ────────────────────────────────────────

    /// <summary>Starts the periodic reachability probe loop. Call from the page's OnAppearing.</summary>
    public void StartConnectionMonitoring()
    {
        if (_statusMonitorCts is not null)
            return;

        _statusMonitorCts = new CancellationTokenSource();
        var token = _statusMonitorCts.Token;
        _ = Task.Run(() => MonitorLoopAsync(token));
    }

    /// <summary>Stops the probe loop. Call from the page's OnDisappearing.</summary>
    public void StopConnectionMonitoring()
    {
        _statusMonitorCts?.Cancel();
        _statusMonitorCts?.Dispose();
        _statusMonitorCts = null;
    }

    /// <summary>Runs one out-of-band probe pass (after add/edit/toggle) without waiting for the next tick.</summary>
    private void TriggerStatusCheck()
    {
        var cts = _statusMonitorCts;
        if (cts is null)
            return;

        var token = cts.Token;
        _ = Task.Run(() => RefreshServerStatusesAsync(token), token);
    }

    private async Task MonitorLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RefreshServerStatusesAsync(ct).ConfigureAwait(false);
                await Task.Delay(StatusCheckInterval, _timeProvider, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Status probing must never take the app down; retry on the next tick.
            }
        }
    }

    /// <summary>Probes every listed server once and updates each item's <see cref="DiscoveryServerItem.ConnectionState"/>.</summary>
    public async Task RefreshServerStatusesAsync(CancellationToken ct = default)
    {
        // At-most-one pass at a time (the loop and TriggerStatusCheck may overlap).
        if (Interlocked.CompareExchange(ref _statusCheckInFlight, 1, 0) != 0)
            return;

        try
        {
            foreach (var item in _statusTargets)
            {
                if (ct.IsCancellationRequested)
                    return;

                if (!item.Enabled)
                {
                    SetConnectionState(item, DiscoveryServerConnectionState.Disabled);
                    continue;
                }

                var host = item.Host?.Trim();
                if (string.IsNullOrWhiteSpace(host) || !int.TryParse(item.Port, out var port) || port is < 1 or > 65535)
                {
                    SetConnectionState(item, DiscoveryServerConnectionState.Unknown);
                    continue;
                }

                // Only show the transient "Checking" state while we have no result yet —
                // flashing it every cycle would make the list flicker.
                if (item.ConnectionState is DiscoveryServerConnectionState.Unknown or DiscoveryServerConnectionState.Disabled)
                    SetConnectionState(item, DiscoveryServerConnectionState.Checking);

                bool reachable;
                try
                {
                    reachable = await _discoveryBridge.IsDiscoveryServerReachableAsync(host, port, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                catch
                {
                    reachable = false;
                }

                SetConnectionState(item, reachable
                    ? DiscoveryServerConnectionState.Connected
                    : DiscoveryServerConnectionState.Unreachable);
            }
        }
        finally
        {
            Interlocked.Exchange(ref _statusCheckInFlight, 0);
        }
    }

    private void SetConnectionState(DiscoveryServerItem item, DiscoveryServerConnectionState state)
    {
        _dispatcher.BeginInvokeOnMainThread(() =>
        {
            if (item.ConnectionState != state)
                item.ConnectionState = state;
        });
    }

    // ── Auto-save ───────────────────────────────────────────────────────────

    partial void OnSelectedSectionChanged(SettingsSection value)
    {
        _ = value;
        OnPropertyChanged(nameof(IsGeneralSectionSelected));
        OnPropertyChanged(nameof(IsAppearanceSectionSelected));
        OnPropertyChanged(nameof(IsDiscoverySectionSelected));
        OnPropertyChanged(nameof(IsDeveloperToolsSectionSelected));
        OnPropertyChanged(nameof(IsAboutSectionSelected));
    }

    partial void OnDeveloperModeEnabledChanged(bool value)
    {
        _ = value;
        _ = PersistAsync();
    }

    partial void OnSelectedThemeOptionChanged(string value)
    {
        _ = value;
        _ = PersistAsync();
    }

    partial void OnSelectedAccentColorChanged(string value)
    {
        _ = value;
        _ = PersistAsync();
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

        _statusTargets = DiscoveryServers.ToList();
    }

    private void OnDiscoveryServerItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // The enable switch is the only row control bound TwoWay; Host/Port/DisplayName are
        // changed through the edit dialog (which persists explicitly) and ConnectionState
        // changes come from the probe loop and must not trigger saves.
        if (e.PropertyName != nameof(DiscoveryServerItem.Enabled))
            return;

        _ = sender;
        _ = PersistAsync();
        TriggerStatusCheck();
    }

    private async Task PersistAsync()
    {
        if (_suppressAutoSave)
            return;

        var discoveryServers = new List<DiscoveryServerPreference>(DiscoveryServers.Count);
        for (var index = 0; index < DiscoveryServers.Count; index++)
            discoveryServers.Add(DiscoveryServers[index].ToPreference(index));

        var snapshot = _validationService.Sanitize(new NdiSettingsSnapshot(
            DeveloperModeEnabled,
            _timeProvider.GetUtcNow().ToUnixTimeMilliseconds(),
            ParseThemeOption(SelectedThemeOption),
            ParseAccentColorOption(SelectedAccentColor),
            discoveryServers));

        if (!_validationService.TryValidateForSave(snapshot, out var error))
        {
            DiscoveryServersValidationMessage = error ?? "The settings are invalid.";
            return;
        }

        try
        {
            await _repository.SaveSettingsAsync(snapshot);
        }
        catch (Exception ex)
        {
            DiscoveryServersValidationMessage = $"Saving settings failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Validates add/edit dialog input. Host is required; an empty port falls back to 5959.
    /// <paramref name="excludeItem"/> skips the row being edited during duplicate detection.
    /// </summary>
    private bool TryParseServerInput(
        string? hostInput,
        string? portInput,
        DiscoveryServerItem? excludeItem,
        out string host,
        out int port,
        out string error)
    {
        host = hostInput?.Trim() ?? string.Empty;
        port = DefaultDiscoveryServerPort;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(host) || !_validationService.IsValidHostOrEmpty(host))
        {
            error = "Discovery server host must be a valid hostname or IP address.";
            return false;
        }

        if (!string.IsNullOrWhiteSpace(portInput))
        {
            if (!int.TryParse(portInput.Trim(), out port) || port is < 1 or > 65535)
            {
                error = "Discovery server port must be a number between 1 and 65535.";
                return false;
            }
        }

        var candidateHost = host;
        var candidatePort = port;
        var duplicateExists = DiscoveryServers.Any(item =>
            !ReferenceEquals(item, excludeItem) &&
            string.Equals(item.Host.Trim(), candidateHost, StringComparison.OrdinalIgnoreCase) &&
            int.TryParse(item.Port, out var existingPort) &&
            existingPort == candidatePort);

        if (duplicateExists)
        {
            error = "Discovery servers cannot contain duplicate host and port combinations.";
            return false;
        }

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
}
