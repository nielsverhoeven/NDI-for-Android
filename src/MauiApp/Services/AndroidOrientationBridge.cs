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
    private readonly IWindowSizeClassService _windowSizeClassService;

    public AndroidOrientationBridge(
        INavigationPolicyService navigationPolicyService,
        IWindowSizeClassService windowSizeClassService)
    {
        _navigationPolicyService = navigationPolicyService;
        _windowSizeClassService = windowSizeClassService;
    }

    public void SyncFromDisplayInfo()
    {
        var displayInfo = DeviceDisplay.Current.MainDisplayInfo;
        var orientation = displayInfo.Width >= displayInfo.Height
            ? DeviceOrientation.Landscape
            : DeviceOrientation.Portrait;

        _navigationPolicyService.UpdateOrientation(orientation);
        FeedWindowSizeClass(displayInfo);
    }

    public void UpdateFromConfiguration(Orientation orientation)
    {
        var mapped = orientation == Orientation.Landscape
            ? DeviceOrientation.Landscape
            : DeviceOrientation.Portrait;

        _navigationPolicyService.UpdateOrientation(mapped);

        // Shell.OnSizeAllocated is unreliable, so drive the window size class from the
        // same DeviceDisplay signal as orientation (fires on launch + every rotation).
        FeedWindowSizeClass(DeviceDisplay.Current.MainDisplayInfo);
    }

    private void FeedWindowSizeClass(DisplayInfo displayInfo)
    {
        if (displayInfo.Density <= 0)
            return;

        // MainDisplayInfo width is in physical pixels; convert to density-independent px.
        var widthDp = displayInfo.Width / displayInfo.Density;
        _windowSizeClassService.UpdateFromWidth(widthDp);
    }
}
