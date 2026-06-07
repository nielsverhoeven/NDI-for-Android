using Android.App;
using Android.OS;
using Android.Content.Res;
using Android.Content.PM;
using AndroidX.Core.View;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using NdiForAndroid.Features.Settings.Services;
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

        // Allow the app to draw behind the status bar so Shell chrome and
        // the status bar background are seamless across the full screen width.
        if (Window is not null)
        {
            WindowCompat.SetDecorFitsSystemWindows(Window, false);
            Window.SetFlags(
                Android.Views.WindowManagerFlags.DrawsSystemBarBackgrounds,
                Android.Views.WindowManagerFlags.DrawsSystemBarBackgrounds);
        }

        SyncNavigationOrientation();
    }

    protected override void OnResume()
    {
        base.OnResume();
        SyncNavigationOrientation();
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
        var bridge = IPlatformApplication.Current?.Services.GetService<IAndroidOrientationBridge>();
        bridge?.UpdateFromConfiguration(newConfig.Orientation);
        var isLandscape = newConfig.Orientation == Orientation.Landscape;
        ResolveLifecycleService()?.NotifyConfigurationChanged(isLandscape);
    }

    private void SyncNavigationOrientation()
    {
        var bridge = IPlatformApplication.Current?.Services.GetService<IAndroidOrientationBridge>();
        bridge?.UpdateFromConfiguration(Resources.Configuration.Orientation);
    }

    private static IAppLifecycleService? ResolveLifecycleService() =>
        IPlatformApplication.Current?.Services.GetService<IAppLifecycleService>();
}
