package com.ndi.sdkbridge

import com.ndi.core.model.NdiSource
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext

interface NdiDiscoveryBridge {
    suspend fun discoverSources(): List<NdiSource>
}

interface NdiViewerBridge {
    fun startReceiver(sourceId: String)

    fun stopReceiver()
}

object NativeNdiBridge : NdiDiscoveryBridge, NdiViewerBridge {

    init {
        runCatching { System.loadLibrary("ndi_bridge") }
    }

    override suspend fun discoverSources(): List<NdiSource> = withContext(Dispatchers.IO) {
        val sourceIds = runCatching { nativeDiscoverSourceIds() }.getOrDefault(emptyArray())
        val displayNames = runCatching { nativeDiscoverDisplayNames() }.getOrDefault(emptyArray())

        sourceIds.mapIndexed { index, sourceId ->
            NdiSource(
                sourceId = sourceId,
                displayName = displayNames.getOrNull(index) ?: sourceId,
                lastSeenAtEpochMillis = System.currentTimeMillis(),
            )
        }
    }

    override fun startReceiver(sourceId: String) {
        runCatching { nativeStartReceiver(sourceId) }
    }

    override fun stopReceiver() {
        runCatching { nativeStopReceiver() }
    }

    private external fun nativeDiscoverSourceIds(): Array<String>

    private external fun nativeDiscoverDisplayNames(): Array<String>

    private external fun nativeStartReceiver(sourceId: String)

    private external fun nativeStopReceiver()
}
