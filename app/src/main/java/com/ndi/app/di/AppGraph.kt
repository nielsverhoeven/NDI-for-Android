package com.ndi.app.di

import android.content.Context
import com.ndi.core.database.NdiDatabase
import com.ndi.feature.ndibrowser.data.repository.NdiDiscoveryRepositoryImpl
import com.ndi.feature.ndibrowser.domain.repository.NdiDiscoveryRepository
import com.ndi.sdkbridge.NativeNdiBridge

class AppGraph private constructor(context: Context) {

    private val database = NdiDatabase.getInstance(context)

    val discoveryRepository: NdiDiscoveryRepository = NdiDiscoveryRepositoryImpl(
        bridge = NativeNdiBridge,
        userSelectionDao = database.userSelectionDao(),
    )

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
