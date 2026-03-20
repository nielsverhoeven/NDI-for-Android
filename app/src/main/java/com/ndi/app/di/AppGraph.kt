package com.ndi.app.di

import android.content.Context
import com.ndi.core.database.NdiDatabase
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
import com.ndi.app.navigation.NdiNavigation
import com.ndi.feature.ndibrowser.home.HomeDependencies
import com.ndi.feature.ndibrowser.output.OutputDependencies
import com.ndi.feature.ndibrowser.source_list.SourceListDependencies
import com.ndi.feature.ndibrowser.viewer.ViewerDependencies
import com.ndi.sdkbridge.NativeNdiBridge

class AppGraph private constructor(context: Context) {

    private val database = NdiDatabase.getInstance(context)

    val discoveryRepository: NdiDiscoveryRepository = NdiDiscoveryRepositoryImpl(
        bridge = NativeNdiBridge,
        userSelectionDao = database.userSelectionDao(),
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

    init {
        SourceListDependencies.discoveryRepositoryProvider = { discoveryRepository }
        SourceListDependencies.userSelectionRepositoryProvider = { userSelectionRepository }
        SourceListDependencies.viewerNavigationRequestProvider = NdiNavigation::viewerRequest
        SourceListDependencies.outputNavigationRequestProvider = NdiNavigation::outputRequest
        ViewerDependencies.viewerRepositoryProvider = { viewerRepository }
        ViewerDependencies.userSelectionRepositoryProvider = { userSelectionRepository }
        OutputDependencies.outputRepositoryProvider = { outputRepository }
        OutputDependencies.outputConfigurationRepositoryProvider = { outputConfigurationRepository }
        OutputDependencies.screenCaptureConsentRepositoryProvider = { screenCaptureConsentRepository }

        // Spec 003: Home dashboard dependencies
        HomeDependencies.homeDashboardRepositoryProvider = { homeDashboardRepository }
        HomeDependencies.streamContinuityRepositoryProvider = { streamContinuityRepository }
        HomeDependencies.viewContinuityRepositoryProvider = { viewContinuityRepository }
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
