package com.ndi.app.di

import android.content.Context
import com.ndi.core.database.NdiDatabase
import com.ndi.feature.ndibrowser.data.repository.NdiDiscoveryRepositoryImpl
import com.ndi.feature.ndibrowser.data.repository.NdiViewerRepositoryImpl
import com.ndi.feature.ndibrowser.data.repository.UserSelectionRepositoryImpl
import com.ndi.feature.ndibrowser.domain.repository.NdiDiscoveryRepository
import com.ndi.feature.ndibrowser.domain.repository.NdiViewerRepository
import com.ndi.feature.ndibrowser.domain.repository.UserSelectionRepository
import com.ndi.app.navigation.NdiNavigation
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

    init {
        SourceListDependencies.discoveryRepositoryProvider = { discoveryRepository }
        SourceListDependencies.userSelectionRepositoryProvider = { userSelectionRepository }
        SourceListDependencies.viewerNavigationRequestProvider = NdiNavigation::viewerRequest
        ViewerDependencies.viewerRepositoryProvider = { viewerRepository }
        ViewerDependencies.userSelectionRepositoryProvider = { userSelectionRepository }
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
