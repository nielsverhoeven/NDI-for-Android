using Android.App;
using Android.OS;
using Android.Content.Res;
using Android.Content.PM;
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
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SyncNavigationOrientation();
    }

    protected override void OnResume()
    {
        base.OnResume();
        SyncNavigationOrientation();
    }

    public override void OnConfigurationChanged(Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);

        var bridge = IPlatformApplication.Current?.Services.GetService<IAndroidOrientationBridge>();
        bridge?.UpdateFromConfiguration(newConfig.Orientation);
    }

    private void SyncNavigationOrientation()
    {
        var bridge = IPlatformApplication.Current?.Services.GetService<IAndroidOrientationBridge>();
        bridge?.UpdateFromConfiguration(Resources.Configuration.Orientation);
    }
}
