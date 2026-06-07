using CommunityToolkit.Mvvm.ComponentModel;
using NdiForAndroid.Features.Settings.Models;

namespace NdiForAndroid.Features.Settings.ViewModels;

public partial class DiscoveryServerItem : ObservableObject
{
    [ObservableProperty]
    private string _host;

    [ObservableProperty]
    private string _port;

    [ObservableProperty]
    private bool _enabled;

    public string EndpointDisplay => $"{Host}:{Port}";

    public DiscoveryServerItem(string host, string port, bool enabled)
    {
        _host = host;
        _port = port;
        _enabled = enabled;
    }

    partial void OnHostChanged(string value)
    {
        _ = value;
        OnPropertyChanged(nameof(EndpointDisplay));
    }

    partial void OnPortChanged(string value)
    {
        _ = value;
        OnPropertyChanged(nameof(EndpointDisplay));
    }

    public DiscoveryServerPreference ToPreference(int order)
    {
        _ = int.TryParse(Port, out var parsedPort);
        return new DiscoveryServerPreference(Host.Trim(), parsedPort, Enabled, order);
    }
}