using Android.App;
using Android.Content;
using Android.OS;
using Android.Content.Res;
using Android.Content.PM;
using AndroidX.Core.View;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui;
using Microsoft.Maui.ApplicationModel;
using NdiForAndroid.Features.DeepLinking;
using NdiForAndroid.Features.DeepLinking.Services;
using NdiForAndroid.Features.Settings.Services;
using NdiForAndroid.Platforms.Android.Services;
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

        // Point libndi.so at our writable config directory before any NDI P/Invoke can load
        // the native library. NdiRuntime writes ndi-config.v1.json (with the discovery-server
        // list) here before NDIlib_initialize; doing the env-set this early guards against the
        // library reading NDI_CONFIG_DIR at load time. Uses the native Os.setenv (managed
        // SetEnvironmentVariable does not reach libndi's getenv). See NdiRuntime.SetNdiConfigDir.
        NdiForAndroid.NdiBridge.NdiRuntime.SetNdiConfigDir(FileSystem.AppDataDirectory);

        // Check if launched from a deep link on startup
        var launchIntent = Intent?.Data;
        if (launchIntent != null
            && string.Equals(launchIntent.Scheme, "ndi", StringComparison.OrdinalIgnoreCase)
            && launchIntent.ToString() is { } launchUri)
        {
            HandleDeepLinkAsync(launchUri).FireAndForget();
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

        // API 33+: the screen-share foreground-service notification is only visible
        // once the user grants notification permission — ask up front.
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
            RequestPostNotificationsPermissionAsync().FireAndForget();
    }

    protected override void OnActivityResult(int requestCode, Result resultCode, Intent? data)
    {
        base.OnActivityResult(requestCode, resultCode, data);

        // MediaProjection consent dialog result for NDI screen capture.
        if (requestCode == AndroidVideoCaptureSource.ScreenCaptureRequestCode)
            AndroidVideoCaptureSource.HandleScreenCaptureResult(resultCode, data);
    }

    private static async Task RequestPostNotificationsPermissionAsync()
    {
        await Permissions.RequestAsync<Permissions.PostNotifications>();
    }

    protected override void OnNewIntent(Intent? intent)
    {
        base.OnNewIntent(intent);

        // Process ndi:// deep links from external apps or shortcuts
        var data = intent?.Data;
        if (data != null
            && string.Equals(data.Scheme, "ndi", StringComparison.OrdinalIgnoreCase)
            && data.ToString() is { } uri)
        {
            HandleDeepLinkAsync(uri).FireAndForget();
        }
    }

    private async Task HandleDeepLinkAsync(string uriString)
    {
        try
        {
            var deepLinkService = IPlatformApplication.Current?.Services.GetService<IDeepLinkService>();
            if (deepLinkService == null)
            {
                ShowToast("Deep link service unavailable.");
                return;
            }

            var success = await deepLinkService.ProcessDeepLinkAsync(uriString);
            if (success) return;

            ShowToast(deepLinkService.LastErrorMessage ?? "Unknown deep link error.");
        }
        catch (Exception ex)
        {
            ShowToast($"Deep link error: {ex.Message}");
        }
    }

    private static void ShowToast(string message)
    {
        var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
        activity?.RunOnUiThread(() =>
            Android.Widget.Toast.MakeText(activity, message, Android.Widget.ToastLength.Long)?.Show());
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
        if (Resources?.Configuration?.Orientation is not { } orientation)
            return;

        var bridge = IPlatformApplication.Current?.Services.GetService<IAndroidOrientationBridge>();
        bridge?.UpdateFromConfiguration(orientation);
    }

    private static IAppLifecycleService? ResolveLifecycleService() =>
        IPlatformApplication.Current?.Services.GetService<IAppLifecycleService>();
}
