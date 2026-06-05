using Android.App;
using Android.Content.PM;
using Android.Content.Res;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using NdiForAndroid.Services;

namespace NdiForAndroid;

[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    Label = "NDI for Android",
    Icon = "@drawable/ndi_logo",
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode |
                           ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnResume()
    {
        base.OnResume();
        ResolveLifecycleService()?.NotifyResumed();
    }

    protected override void OnPause()
    {
        ResolveLifecycleService()?.NotifyPaused();
        base.OnPause();
    }

    public override void OnConfigurationChanged(Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);
        var isLandscape = newConfig.Orientation == Orientation.Landscape;
        ResolveLifecycleService()?.NotifyConfigurationChanged(isLandscape);
    }

    private static IAppLifecycleService? ResolveLifecycleService() =>
        IPlatformApplication.Current?.Services.GetService<IAppLifecycleService>();
}
