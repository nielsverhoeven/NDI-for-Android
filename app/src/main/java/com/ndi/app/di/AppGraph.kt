package com.ndi.app.di

import android.content.Context
import com.ndi.core.database.NdiDatabase
import com.ndi.core.model.NdiOverlayMode
import com.ndi.feature.ndibrowser.data.OutputRecoveryCoordinator
import com.ndi.feature.ndibrowser.data.OutputSessionCoordinator
import com.ndi.feature.ndibrowser.data.mapper.OutputSessionMapper
import com.ndi.feature.ndibrowser.data.repository.HomeDashboardRepositoryImpl
import com.ndi.feature.ndibrowser.data.repository.NdiDiscoveryRepositoryImpl
import com.ndi.feature.ndibrowser.data.repository.NdiOutputRepositoryImpl
import com.ndi.feature.ndibrowser.data.repository.NdiViewerRepositoryImpl
import com.ndi.feature.ndibrowser.data.repository.OutputConfigurationRepositoryImpl
import com.ndi.feature.ndibrowser.data.repository.ScreenCaptureConsentRepositoryImpl
import com.ndi.feature.ndibrowser.data.repository.StreamContinuityRepositoryImpl
import com.ndi.feature.ndibrowser.data.repository.TopLevelNavigationRepositoryImpl
import com.ndi.feature.ndibrowser.data.repository.UserSelectionRepositoryImpl
import com.ndi.feature.ndibrowser.data.repository.ViewContinuityRepositoryImpl
import com.ndi.feature.ndibrowser.domain.repository.HomeDashboardRepository
import com.ndi.feature.ndibrowser.domain.repository.NdiDiscoveryRepository
import com.ndi.feature.ndibrowser.domain.repository.NdiOutputRepository
import com.ndi.feature.ndibrowser.domain.repository.NdiViewerRepository
import com.ndi.feature.ndibrowser.domain.repository.OutputConfigurationRepository
import com.ndi.feature.ndibrowser.domain.repository.ScreenCaptureConsentRepository
import com.ndi.feature.ndibrowser.domain.repository.StreamContinuityRepository
import com.ndi.feature.ndibrowser.domain.repository.TopLevelNavigationRepository
import com.ndi.feature.ndibrowser.domain.repository.UserSelectionRepository
import com.ndi.feature.ndibrowser.domain.repository.ViewContinuityRepository
import com.ndi.feature.themeeditor.data.repository.ThemeEditorRepositoryImpl
import com.ndi.feature.themeeditor.domain.repository.ThemeEditorRepository
import com.ndi.feature.themeeditor.ThemeEditorDependencies
import com.ndi.feature.themeeditor.ThemeEditorTelemetryEmitter
import com.ndi.app.theme.AppThemeCoordinator
import com.ndi.app.navigation.NdiNavigation
import com.ndi.feature.ndibrowser.data.repository.DeveloperDiagnosticsRepositoryImpl
import com.ndi.feature.ndibrowser.data.repository.DiscoveryServerRepositoryImpl
import com.ndi.feature.ndibrowser.data.repository.NdiDiscoveryConfigRepositoryImpl
import com.ndi.feature.ndibrowser.data.repository.NdiSettingsRepositoryImpl
import com.ndi.feature.ndibrowser.domain.repository.DeveloperDiagnosticsRepository
import com.ndi.feature.ndibrowser.domain.repository.DiscoveryServerRepository
import com.ndi.feature.ndibrowser.domain.repository.NdiDiscoveryConfigRepository
import com.ndi.feature.ndibrowser.domain.repository.NdiSettingsRepository
import com.ndi.feature.ndibrowser.home.HomeDependencies
import com.ndi.feature.ndibrowser.output.OutputDependencies
import com.ndi.feature.ndibrowser.settings.DeveloperOverlayStateMapper
import com.ndi.feature.ndibrowser.settings.OverlayDisplayState
import com.ndi.feature.ndibrowser.settings.OverlayLogRedactor
import com.ndi.feature.ndibrowser.settings.SettingsDependencies
import com.ndi.feature.ndibrowser.settings.SettingsTelemetry
import com.ndi.feature.ndibrowser.source_list.SourceListDependencies
import com.ndi.feature.ndibrowser.viewer.ViewerDependencies
import com.ndi.sdkbridge.NativeNdiBridge
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.onEach
import kotlinx.coroutines.flow.stateIn

class AppGraph private constructor(context: Context) {

    private val appScope = CoroutineScope(Dispatchers.Default + SupervisorJob())

    init {
        NativeNdiBridge.initialize(context.applicationContext)
    }

    private val database = NdiDatabase.getInstance(context)

    // ---- Spec 006: Settings Menu repositories ----

    val settingsRepository: NdiSettingsRepository = NdiSettingsRepositoryImpl(
        settingsDao = database.settingsPreferenceDao(),
        discoveryServerDao = database.discoveryServerDao(),
    )

    val discoveryServerRepository: DiscoveryServerRepository = DiscoveryServerRepositoryImpl(
        discoveryServerDao = database.discoveryServerDao(),
    )

    val themeEditorRepository: ThemeEditorRepository = ThemeEditorRepositoryImpl(
        settingsDao = database.settingsPreferenceDao(),
    )

    val appThemeCoordinator: AppThemeCoordinator = AppThemeCoordinator(
        themeEditorRepository = themeEditorRepository,
    )

    val discoveryConfigRepository: NdiDiscoveryConfigRepository = NdiDiscoveryConfigRepositoryImpl(
        settingsRepository = settingsRepository,
    )

    val discoveryRepository: NdiDiscoveryRepository = NdiDiscoveryRepositoryImpl(
        bridge = NativeNdiBridge,
        userSelectionDao = database.userSelectionDao(),
        discoveryConfigRepository = discoveryConfigRepository,
    )

    val userSelectionRepository: UserSelectionRepository = UserSelectionRepositoryImpl(
        userSelectionDao = database.userSelectionDao(),
    )

    val viewerRepository: NdiViewerRepository = NdiViewerRepositoryImpl(
        bridge = NativeNdiBridge,
        viewerSessionDao = database.viewerSessionDao(),
    )

    val screenCaptureConsentRepository: ScreenCaptureConsentRepository = ScreenCaptureConsentRepositoryImpl()

    val outputRepository: NdiOutputRepository = NdiOutputRepositoryImpl(
        outputSessionDao = database.outputSessionDao(),
        outputBridge = NativeNdiBridge,
        discoveryConfigRepository = discoveryConfigRepository,
        screenCaptureConsentRepository = screenCaptureConsentRepository,
        mapper = OutputSessionMapper(),
        coordinator = OutputSessionCoordinator(),
        recoveryCoordinator = OutputRecoveryCoordinator(),
    )

    val outputConfigurationRepository: OutputConfigurationRepository = OutputConfigurationRepositoryImpl(
        outputConfigurationDao = database.outputConfigurationDao(),
    )

    // ---- Spec 003: Three-Screen Navigation repositories ----

    val topLevelNavigationRepository: TopLevelNavigationRepository = TopLevelNavigationRepositoryImpl()

    val homeDashboardRepository: HomeDashboardRepository = HomeDashboardRepositoryImpl(
        outputRepository = outputRepository,
        viewerRepository = viewerRepository,
        userSelectionRepository = userSelectionRepository,
    )

    val streamContinuityRepository: StreamContinuityRepository = StreamContinuityRepositoryImpl(
        outputRepository = outputRepository,
    )

    val viewContinuityRepository: ViewContinuityRepository = ViewContinuityRepositoryImpl(
        viewerRepository = viewerRepository,
        userSelectionRepository = userSelectionRepository,
    )

    val developerDiagnosticsRepository: DeveloperDiagnosticsRepository = DeveloperDiagnosticsRepositoryImpl(
        viewerRepository = viewerRepository,
        outputRepository = outputRepository,
    )

    private var previousOverlayMode: NdiOverlayMode? = null

    private val overlayDisplayStateFlow: StateFlow<OverlayDisplayState?> =
        combine(
            settingsRepository.observeSettings(),
            developerDiagnosticsRepository.observeOverlayState(),
        ) { settings, overlayState ->
            val redactedLogs = overlayState.recentLogs.map { log ->
                OverlayLogRedactor.redact(log.messageRedacted)
            }
            val linesRedacted = overlayState.recentLogs.count { log ->
                OverlayLogRedactor.redact(log.messageRedacted) != log.messageRedacted
            }
            if (linesRedacted > 0) {
                SettingsDependencies.telemetryEmitter.emit(
                    SettingsTelemetry.overlayLogRedactionApplied(linesRedacted),
                )
            }

            DeveloperOverlayStateMapper.map(
                developerModeEnabled = settings.developerModeEnabled,
                streamStatus = overlayState.streamStatusLabel.takeIf { it.isNotBlank() },
                sessionId = OverlayLogRedactor.redactSessionId(overlayState.sessionId),
                recentLogs = redactedLogs,
            )
        }.onEach { overlayDisplayState ->
            val currentMode = overlayDisplayState?.mode ?: NdiOverlayMode.DISABLED
            val previousMode = previousOverlayMode
            if (previousMode != null && previousMode != currentMode) {
                SettingsDependencies.telemetryEmitter.emit(
                    SettingsTelemetry.developerOverlayStateChanged(previousMode, currentMode),
                )
            }
            previousOverlayMode = currentMode
        }.stateIn(
            scope = appScope,
            started = SharingStarted.Eagerly,
            initialValue = null,
        )

    init {
        SourceListDependencies.discoveryRepositoryProvider = { discoveryRepository }
        SourceListDependencies.userSelectionRepositoryProvider = { userSelectionRepository }
        SourceListDependencies.viewerNavigationRequestProvider = NdiNavigation::viewerRequest
        SourceListDependencies.outputNavigationRequestProvider = NdiNavigation::outputRequest
        SourceListDependencies.overlayStateProvider = { overlayDisplayStateFlow }
        ViewerDependencies.viewerRepositoryProvider = { viewerRepository }
        ViewerDependencies.userSelectionRepositoryProvider = { userSelectionRepository }
        ViewerDependencies.overlayStateProvider = { overlayDisplayStateFlow }
        OutputDependencies.outputRepositoryProvider = { outputRepository }
        OutputDependencies.outputConfigurationRepositoryProvider = { outputConfigurationRepository }
        OutputDependencies.screenCaptureConsentRepositoryProvider = { screenCaptureConsentRepository }
        OutputDependencies.streamContinuityRepositoryProvider = { streamContinuityRepository }
        OutputDependencies.overlayStateProvider = { overlayDisplayStateFlow }

        // Spec 003: Home dashboard dependencies
        HomeDependencies.homeDashboardRepositoryProvider = { homeDashboardRepository }
        HomeDependencies.streamContinuityRepositoryProvider = { streamContinuityRepository }
        HomeDependencies.viewContinuityRepositoryProvider = { viewContinuityRepository }

        // Spec 006: Settings dependencies
        SettingsDependencies.settingsRepositoryProvider = { settingsRepository }
        SettingsDependencies.developerDiagnosticsRepositoryProvider = { developerDiagnosticsRepository }
        SettingsDependencies.overlayStateProvider = { overlayDisplayStateFlow }
        // Spec 018: Discovery server dependencies
        SettingsDependencies.discoveryServerRepositoryProvider = { discoveryServerRepository }

        ThemeEditorDependencies.themeEditorRepositoryProvider = { themeEditorRepository }
        ThemeEditorDependencies.telemetryEmitter = ThemeEditorTelemetryEmitter { event ->
            SettingsDependencies.telemetryEmitter.emit(event)
        }
    }

    companion object {
        @Volatile
        private var instance: AppGraph? = null

        fun initialize(context: Context): AppGraph {
            return instance ?: synchronized(this) {
                instance ?: AppGraph(context.applicationContext).also { instance = it }
            }
        }

        fun get(): AppGraph {
            return requireNotNull(instance) { "AppGraph has not been initialized." }
        }
    }
}
