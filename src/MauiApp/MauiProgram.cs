using Microsoft.Extensions.Logging;
using NdiForAndroid.Data;
using NdiForAndroid.Features.Navigation.Services;
using NdiForAndroid.Features.Navigation.ViewModels;
using NdiForAndroid.Features.Output.ViewModels;
using NdiForAndroid.Features.Settings.Repositories;
using NdiForAndroid.Features.Settings.Services;
using NdiForAndroid.Features.Settings.ViewModels;
using NdiForAndroid.Features.Sources.Repositories;
using NdiForAndroid.Features.Sources.ViewModels;
using NdiForAndroid.Features.Viewer.ViewModels;
using NdiForAndroid.NdiBridge;
using NdiForAndroid.Services;

#if ANDROID
using NdiForAndroid.Platforms.Android.Services;
#endif

namespace NdiForAndroid;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();

        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

#if DEBUG
        builder.Logging.AddDebug();
#endif

        // Data
        builder.Services.AddSingleton<NdiDatabase>();

        // NDI Bridge
        builder.Services.AddSingleton<INdiDiscoveryBridge, NdiDiscoveryBridge>();
        builder.Services.AddSingleton<INdiViewerBridge, NdiViewerBridge>();
        builder.Services.AddSingleton<INdiOutputBridge, NdiOutputBridge>();

        // Repositories
        builder.Services.AddSingleton<ISourceRepository, SourceRepository>();
        builder.Services.AddSingleton<ISettingsRepository, SettingsRepository>();
        builder.Services.AddSingleton<ISettingsValidationService, SettingsValidationService>();
        builder.Services.AddSingleton<IDiscoverySettingsOrchestrator, DiscoverySettingsOrchestrator>();

        // Services
        builder.Services.AddSingleton<ITelemetryService, TelemetryService>();
        builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
        builder.Services.AddSingleton<INavigationPolicyService, NavigationPolicyService>();
        builder.Services.AddSingleton<INavigationHandoffService, NdiNavigationHandoffService>();
        builder.Services.AddSingleton<IAndroidOrientationBridge, AndroidOrientationBridge>();
        builder.Services.AddSingleton<IAppLifecycleService, AppLifecycleService>();

#if ANDROID
        builder.Services.AddSingleton<IScreenSharePlatformService, AndroidScreenSharePlatformService>();
        builder.Services.AddSingleton<ISettingsPlatformService, AndroidSettingsPlatformService>();
#else
        builder.Services.AddSingleton<IScreenSharePlatformService, NoopScreenSharePlatformService>();
        builder.Services.AddSingleton<ISettingsPlatformService, DefaultSettingsPlatformService>();
#endif

        // ViewModels
        builder.Services.AddSingleton<AdaptiveShellStateViewModel>();
        builder.Services.AddTransient<SourceListViewModel>();
        builder.Services.AddTransient<ViewerViewModel>();
        builder.Services.AddTransient<OutputViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        // Views
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<Features.Sources.Views.SourceListPage>();
        builder.Services.AddTransient<Features.Viewer.Views.ViewerPage>();
        builder.Services.AddTransient<Features.Output.Views.OutputPage>();
        builder.Services.AddTransient<Features.Settings.Views.SettingsPage>();

        return builder.Build();
    }
}
