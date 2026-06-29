using Android.App;
using Android.Content;
using Android.OS;
using Android.Content.Res;
using Android.Content.PM;
using AndroidX.Core.View;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using NdiForAndroid.Features.DeepLinking;
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
[IntentFilter(
    new[] { Intent.ActionView },
    Categories = new[] { Intent.CategoryDefault, Intent.CategoryBrowsable },
    DataScheme = "ndi",
    DataHost = "*")]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Check if launched from a deep link on startup
        var launchIntent = Intent?.Data;
        if (launchIntent != null && string.Equals(launchIntent.Scheme, "ndi", StringComparison.OrdinalIgnoreCase))
        {
            HandleDeepLinkAsync(launchIntent.ToString()).ConfigureAwait(continueOnCapturedContext: false);
        }

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

    protected override void OnNewIntent(Intent intent)
    {
        base.OnNewIntent(intent);

        // Process ndi:// deep links from external apps or shortcuts
        var data = intent?.Data;
        if (data != null && string.Equals(data.Scheme, "ndi", StringComparison.OrdinalIgnoreCase))
        {
            HandleDeepLinkAsync(data.ToString()).ConfigureAwait(continueOnCapturedContext: false);
        }
    }

    private async void HandleDeepLinkAsync(string uriString)
    {
        try
        {
            var deepLinkService = IPlatformApplication.Current?.Services.GetService<IDeepLinkService>();
            if (deepLinkService == null)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                    Microsoft.Maui.ApplicationModel.Platform.CurrentActivity?.RunOnUiThread(() =>
                        Android.Widget.Toast.MakeText(Microsoft.Maui.ApplicationModel.Platform.CurrentActivity, "Deep link service unavailable.", Android.Widget.ToastLength.Long).Show()));
                return;
            }

            var success = await deepLinkService.ProcessDeepLinkAsync(uriString);
            if (success) return;

            // Show error message
            var msg = deepLinkService.LastErrorMessage ?? "Unknown deep link error.";
            await MainThread.InvokeOnMainThreadAsync(() =>
                Android.Widget.Toast.MakeText(Microsoft.Maui.ApplicationModel.Platform.CurrentActivity, msg, Android.Widget.ToastLength.Long).Show());
        }
        catch (Exception ex)
        {
            await MainThread.InvokeOnMainThreadAsync(() =>
                Android.Widget.Toast.MakeText(Microsoft.Maui.ApplicationModel.Platform.CurrentActivity, $"Deep link error: {ex.Message}", Android.Widget.ToastLength.Long).Show());
        }
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
