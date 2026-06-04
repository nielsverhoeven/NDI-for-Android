using Android.Content.Res;
using Microsoft.Maui.Devices;
using NdiForAndroid.Features.Navigation.Models;
using NdiForAndroid.Features.Navigation.Services;

namespace NdiForAndroid.Services;

public interface IAndroidOrientationBridge
{
    void SyncFromDisplayInfo();
    void UpdateFromConfiguration(Orientation orientation);
}

public sealed class AndroidOrientationBridge : IAndroidOrientationBridge
{
    private readonly INavigationPolicyService _navigationPolicyService;

    public AndroidOrientationBridge(INavigationPolicyService navigationPolicyService)
    {
        _navigationPolicyService = navigationPolicyService;
    }

    public void SyncFromDisplayInfo()
    {
        var orientation = DeviceDisplay.Current.MainDisplayInfo.Orientation == DisplayOrientation.Landscape
            ? DeviceOrientation.Landscape
            : DeviceOrientation.Portrait;

        _navigationPolicyService.UpdateOrientation(orientation);
    }

    public void UpdateFromConfiguration(Orientation orientation)
    {
        var mapped = orientation == Orientation.Landscape
            ? DeviceOrientation.Landscape
            : DeviceOrientation.Portrait;

        _navigationPolicyService.UpdateOrientation(mapped);
    }
}
