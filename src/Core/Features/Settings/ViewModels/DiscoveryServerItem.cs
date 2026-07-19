using CommunityToolkit.Mvvm.ComponentModel;
using NdiForAndroid.Features.Settings.Models;

namespace NdiForAndroid.Features.Settings.ViewModels;

public partial class DiscoveryServerItem : ObservableObject
{
    [ObservableProperty]
    private string? _displayName;

    [ObservableProperty]
    private string _host;

    [ObservableProperty]
    private string _port;

    [ObservableProperty]
    private bool _enabled;

    [ObservableProperty]
    private DiscoveryServerConnectionState _connectionState = DiscoveryServerConnectionState.Unknown;

    /// <summary>Name shown in the server list — the optional display name, falling back to the hostname.</summary>
    public string NameDisplay => string.IsNullOrWhiteSpace(DisplayName) ? Host : DisplayName.Trim();

    public string EndpointDisplay => $"{Host}:{Port}";

    public string ConnectionStateDisplay => ConnectionState switch
    {
        DiscoveryServerConnectionState.Connected => "Connected",
        DiscoveryServerConnectionState.Unreachable => "Unreachable",
        DiscoveryServerConnectionState.Checking => "Checking…",
        DiscoveryServerConnectionState.Disabled => "Disabled",
        _ => "Unknown",
    };

    public DiscoveryServerItem(string host, string port, bool enabled, string? displayName = null)
    {
        _host = host;
        _port = port;
        _enabled = enabled;
        _displayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
    }

    partial void OnDisplayNameChanged(string? value)
    {
        _ = value;
        OnPropertyChanged(nameof(NameDisplay));
    }

    partial void OnHostChanged(string value)
    {
        _ = value;
        OnPropertyChanged(nameof(EndpointDisplay));
        OnPropertyChanged(nameof(NameDisplay));
    }

    partial void OnPortChanged(string value)
    {
        _ = value;
        OnPropertyChanged(nameof(EndpointDisplay));
    }

    partial void OnConnectionStateChanged(DiscoveryServerConnectionState value)
    {
        _ = value;
        OnPropertyChanged(nameof(ConnectionStateDisplay));
    }

    public DiscoveryServerPreference ToPreference(int order)
    {
        _ = int.TryParse(Port, out var parsedPort);
        return new DiscoveryServerPreference(
            Host.Trim(),
            parsedPort,
            Enabled,
            order,
            string.IsNullOrWhiteSpace(DisplayName) ? null : DisplayName.Trim());
    }
}
