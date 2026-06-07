using NdiForAndroid.Features.Settings.ViewModels;
using System.ComponentModel;
using Xunit;

namespace MauiApp.Tests.Features.Settings;

/// <summary>
/// Tests for the DiscoveryServerItem ObservableObject —
/// verifies reactive property-change notifications and ToPreference() mapping.
/// </summary>
public sealed class DiscoveryServerItemTests
{
    // ─── Constructor ───────────────────────────────────────────────────────────

    [Fact]
    public void Constructor_SetsHostPortAndEnabled()
    {
        var sut = new DiscoveryServerItem("my-host", "5960", true);

        Assert.Equal("my-host", sut.Host);
        Assert.Equal("5960", sut.Port);
        Assert.True(sut.Enabled);
    }

    [Fact]
    public void Constructor_False_SetsEnabledFalse()
    {
        var sut = new DiscoveryServerItem("h", "80", false);

        Assert.False(sut.Enabled);
    }

    // ─── EndpointDisplay ───────────────────────────────────────────────────────

    [Fact]
    public void EndpointDisplay_ReturnsHostColonPort()
    {
        var sut = new DiscoveryServerItem("192.168.0.1", "5960", true);

        Assert.Equal("192.168.0.1:5960", sut.EndpointDisplay);
    }

    // ─── PropertyChanged – Host ────────────────────────────────────────────────

    [Fact]
    public void SettingHost_RaisesPropertyChangedForHost()
    {
        var sut = new DiscoveryServerItem("old-host", "5960", true);
        var raised = CollectChangedProperties(sut, () => sut.Host = "new-host");

        Assert.Contains("Host", raised);
    }

    [Fact]
    public void SettingHost_RaisesPropertyChangedForEndpointDisplay()
    {
        var sut = new DiscoveryServerItem("old-host", "5960", true);
        var raised = CollectChangedProperties(sut, () => sut.Host = "new-host");

        Assert.Contains("EndpointDisplay", raised);
    }

    [Fact]
    public void SettingHost_UpdatesEndpointDisplay()
    {
        var sut = new DiscoveryServerItem("old", "1234", true);
        sut.Host = "new-server";

        Assert.Equal("new-server:1234", sut.EndpointDisplay);
    }

    // ─── PropertyChanged – Port ────────────────────────────────────────────────

    [Fact]
    public void SettingPort_RaisesPropertyChangedForPort()
    {
        var sut = new DiscoveryServerItem("host", "5960", true);
        var raised = CollectChangedProperties(sut, () => sut.Port = "9000");

        Assert.Contains("Port", raised);
    }

    [Fact]
    public void SettingPort_RaisesPropertyChangedForEndpointDisplay()
    {
        var sut = new DiscoveryServerItem("host", "5960", true);
        var raised = CollectChangedProperties(sut, () => sut.Port = "9000");

        Assert.Contains("EndpointDisplay", raised);
    }

    [Fact]
    public void SettingPort_UpdatesEndpointDisplay()
    {
        var sut = new DiscoveryServerItem("srv", "5960", true);
        sut.Port = "7001";

        Assert.Equal("srv:7001", sut.EndpointDisplay);
    }

    // ─── PropertyChanged – Enabled ─────────────────────────────────────────────

    [Fact]
    public void SettingEnabled_RaisesPropertyChangedForEnabled()
    {
        var sut = new DiscoveryServerItem("host", "5960", false);
        var raised = CollectChangedProperties(sut, () => sut.Enabled = true);

        Assert.Contains("Enabled", raised);
    }

    // ─── ToPreference ──────────────────────────────────────────────────────────

    [Fact]
    public void ToPreference_ReturnsCorrectMappingWithGivenOrder()
    {
        var sut = new DiscoveryServerItem("  192.168.1.1  ", "5961", true);

        var pref = sut.ToPreference(3);

        Assert.Equal("192.168.1.1", pref.Host); // trimmed
        Assert.Equal(5961, pref.Port);
        Assert.True(pref.Enabled);
        Assert.Equal(3, pref.Order);
    }

    [Fact]
    public void ToPreference_NonNumericPort_ReturnsZeroPort()
    {
        var sut = new DiscoveryServerItem("host", "not-a-number", true);

        var pref = sut.ToPreference(0);

        Assert.Equal(0, pref.Port);
    }

    [Fact]
    public void ToPreference_OrderZero_ReturnsOrderZero()
    {
        var sut = new DiscoveryServerItem("host", "5960", false);

        var pref = sut.ToPreference(0);

        Assert.Equal(0, pref.Order);
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    private static List<string> CollectChangedProperties(INotifyPropertyChanged source, Action mutate)
    {
        var names = new List<string>();
        source.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not null)
                names.Add(e.PropertyName);
        };
        mutate();
        return names;
    }
}
