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

interface NdiOutputBridge {
    suspend fun isSourceReachable(sourceId: String): Boolean

    fun startSender(sourceId: String, streamName: String)

    fun stopSender()
}

object NativeNdiBridge : NdiDiscoveryBridge, NdiViewerBridge, NdiOutputBridge {

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

    override suspend fun isSourceReachable(sourceId: String): Boolean = withContext(Dispatchers.IO) {
        discoverSources().any { it.sourceId == sourceId }
    }

    override fun startSender(sourceId: String, streamName: String) {
        runCatching { nativeStartSender(sourceId, streamName) }
    }

    override fun stopSender() {
        runCatching { nativeStopSender() }
    }

    private external fun nativeDiscoverSourceIds(): Array<String>

    private external fun nativeDiscoverDisplayNames(): Array<String>

    private external fun nativeStartReceiver(sourceId: String)

    private external fun nativeStopReceiver()

    private external fun nativeStartSender(sourceId: String, streamName: String)

    private external fun nativeStopSender()
}
