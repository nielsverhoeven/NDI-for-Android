using Microsoft.Extensions.Logging;
using NdiForAndroid.Data;
using NdiForAndroid.Features.AppState.Repositories;
using NdiForAndroid.Features.ConnectionHistory;
using NdiForAndroid.Features.ConnectionHistory.Services;
using NdiForAndroid.Features.DeepLinking;
using NdiForAndroid.Features.DeepLinking.Services;
using NdiForAndroid.Features.Home.ViewModels;
using NdiForAndroid.Features.Navigation.Services;
using NdiForAndroid.Features.Navigation.ViewModels;
using NdiForAndroid.Features.Output.Repositories;
using NdiForAndroid.Features.Output.ViewModels;
using NdiForAndroid.Features.Settings.Repositories;
using NdiForAndroid.Features.Settings.Services;
using NdiForAndroid.Features.Settings.ViewModels;
using NdiForAndroid.Features.Sources.Repositories;
using NdiForAndroid.Features.Sources.ViewModels;
using NdiForAndroid.Features.Viewer.ViewModels;
using NdiForAndroid.NdiBridge;
using NdiForAndroid.Services;
using SkiaSharp.Views.Maui.Controls.Hosting;

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
            .UseSkiaSharp()
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
        builder.Services.AddSingleton<NdiRuntime>();
        builder.Services.AddSingleton<INdiDiscoveryBridge, NdiDiscoveryBridge>();
        builder.Services.AddSingleton<INdiViewerBridge, NdiViewerBridge>();
        builder.Services.AddSingleton<INdiOutputBridge, NdiOutputBridge>();

        // Repositories
        builder.Services.AddSingleton<ISourceRepository, SourceRepository>();
        builder.Services.AddSingleton<IOutputConfigurationRepository, OutputConfigurationRepository>();
        builder.Services.AddSingleton<ISettingsRepository, SettingsRepository>();
        builder.Services.AddSingleton<ISettingsValidationService, SettingsValidationService>();
        builder.Services.AddSingleton<IDiscoverySettingsOrchestrator, DiscoverySettingsOrchestrator>();
        builder.Services.AddSingleton<IAppearanceService, MauiAppearanceService>();

        // Services
        var ndiDbPath = Path.Combine(FileSystem.AppDataDirectory, "ndi.db3");
        builder.Services.AddSingleton<IAppStateRepository>(sp =>
            new AppStateRepository(ndiDbPath));
        builder.Services.AddSingleton<IConnectionHistoryService, ConnectionHistoryService>();
        builder.Services.AddSingleton<IDeepLinkService, DeepLinkService>();
        builder.Services.AddSingleton<ITelemetryService, TelemetryService>();
        builder.Services.AddSingleton<INavigationService, ShellNavigationService>();
        builder.Services.AddSingleton<INavigationPolicyService, NavigationPolicyService>();
        builder.Services.AddSingleton<INavigationHandoffService, NdiNavigationHandoffService>();
        builder.Services.AddSingleton<IAndroidOrientationBridge, AndroidOrientationBridge>();
        builder.Services.AddSingleton<IAppLifecycleService, AppLifecycleService>();
        builder.Services.AddSingleton<IDiscoveryRefreshService, DiscoveryRefreshService>();
        builder.Services.AddSingleton<IMainThreadDispatcher>(sp =>
#if ANDROID
            (IMainThreadDispatcher)new AndroidMainThreadDispatcher()
#else
            (IMainThreadDispatcher)new DefaultMainThreadDispatcher()
#endif
        );
        builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);

        // Developer-mode diagnostics (#241): overlay service + log page. The FPS/discovery
        // producers hook in with the real NDI bridge stats (#277).
        builder.Services.AddSingleton<Features.DiagOverlay.Services.IDiagnosticOverlayService,
            Features.DiagOverlay.DiagnosticOverlayService>();
        builder.Services.AddTransient<Features.DiagOverlay.ViewModels.DiagnosticLogViewModel>();
        builder.Services.AddTransient<Features.DiagOverlay.Views.DiagnosticLogPage>();

#if ANDROID
        builder.Services.AddSingleton<IMulticastLockService, AndroidMulticastLockService>();
        builder.Services.AddSingleton<IScreenSharePlatformService, AndroidScreenSharePlatformService>();
        builder.Services.AddSingleton<ISettingsPlatformService, AndroidSettingsPlatformService>();
        builder.Services.AddSingleton<INdiPlatformBootstrap, AndroidNsdBootstrap>();
        builder.Services.AddSingleton<IAudioPlaybackSink, AndroidAudioPlaybackSink>();
        builder.Services.AddSingleton<IVideoCaptureSource, AndroidVideoCaptureSource>();
        builder.Services.AddSingleton<IAudioCaptureSource, AndroidMicrophoneCaptureSource>();
#else
        builder.Services.AddSingleton<IMulticastLockService, NoopMulticastLockService>();
        builder.Services.AddSingleton<IScreenSharePlatformService, NoopScreenSharePlatformService>();
        builder.Services.AddSingleton<ISettingsPlatformService, DefaultSettingsPlatformService>();
        builder.Services.AddSingleton<INdiPlatformBootstrap, DefaultNdiPlatformBootstrap>();
        builder.Services.AddSingleton<IAudioPlaybackSink, NoopAudioPlaybackSink>();
        builder.Services.AddSingleton<IVideoCaptureSource, NoopVideoCaptureSource>();
        builder.Services.AddSingleton<IAudioCaptureSource, NoopAudioCaptureSource>();
#endif

        // ViewModels
        builder.Services.AddSingleton<AdaptiveShellStateViewModel>();
        builder.Services.AddSingleton<SourceListViewModel>();  // Singleton: subscribes to singleton IDiscoveryRefreshService
        builder.Services.AddTransient<HomeViewModel>();
        builder.Services.AddTransient<ViewerViewModel>();
        builder.Services.AddTransient<OutputViewModel>();
        builder.Services.AddTransient<SettingsViewModel>();

        // Views
        builder.Services.AddSingleton<AppShell>();
        builder.Services.AddTransient<Features.Home.Views.HomePage>();
        builder.Services.AddSingleton<Features.Sources.Views.SourceListPage>();  // Singleton: matches ViewModel lifetime (C1)
        builder.Services.AddTransient<Features.Viewer.Views.ViewerPage>();
        builder.Services.AddTransient<Features.Output.Views.OutputPage>();
        builder.Services.AddTransient<Features.Settings.Views.SettingsPage>();

        return builder.Build();
    }
}
