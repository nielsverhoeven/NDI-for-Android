package com.ndi.sdkbridge

import com.ndi.core.model.NdiSource
import java.net.HttpURLConnection
import java.net.URL
import java.util.UUID
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext
import org.json.JSONArray
import org.json.JSONObject

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

    fun startLocalScreenShareSender(streamName: String)

    fun stopLocalScreenShareSender()
}

object NativeNdiBridge : NdiDiscoveryBridge, NdiViewerBridge, NdiOutputBridge {

    private const val RELAY_BASE_URL = "http://10.0.2.2:17455"
    private const val HEARTBEAT_INTERVAL_MS = 1_000L

    private data class LocalRelaySender(
        val sourceId: String,
        val streamName: String,
    )

    // Keep a stable per-process host id so restart+rename updates one source instead of creating duplicates.
    private val relayHostInstanceId: String = UUID.randomUUID().toString()

    private val relayScope = CoroutineScope(SupervisorJob() + Dispatchers.IO)

    @Volatile
    private var activeLocalSender: LocalRelaySender? = null

    @Volatile
    private var activeLocalSenderHeartbeat = null as kotlinx.coroutines.Job?

    init {
        runCatching { System.loadLibrary("ndi_bridge") }
    }

    override suspend fun discoverSources(): List<NdiSource> = withContext(Dispatchers.IO) {
        val relaySources = runCatching { discoverRelaySources() }.getOrDefault(emptyList())
        if (relaySources.isNotEmpty()) {
            return@withContext relaySources
        }

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

    override fun startLocalScreenShareSender(streamName: String) {
        val normalizedName = streamName.trim().ifBlank { "NDI Output" }
        val session = LocalRelaySender(
            sourceId = "relay-screen:$relayHostInstanceId",
            streamName = normalizedName,
        )

        activeLocalSenderHeartbeat?.cancel()
        activeLocalSender = session
        activeLocalSenderHeartbeat = relayScope.launch {
            while (isActive) {
                runCatching { announceRelaySource(session) }
                delay(HEARTBEAT_INTERVAL_MS)
            }
        }

        runCatching { nativeStartLocalScreenShareSender(normalizedName) }
    }

    override fun stopLocalScreenShareSender() {
        val session = activeLocalSender
        activeLocalSender = null
        activeLocalSenderHeartbeat?.cancel()
        activeLocalSenderHeartbeat = null
        if (session != null) {
            runCatching { revokeRelaySource(session.sourceId) }
        }
        runCatching { nativeStopLocalScreenShareSender() }
    }

    private fun discoverRelaySources(): List<NdiSource> {
        val connection = (URL("$RELAY_BASE_URL/sources").openConnection() as HttpURLConnection).apply {
            requestMethod = "GET"
            connectTimeout = 1_000
            readTimeout = 1_500
        }
        return try {
            if (connection.responseCode !in 200..299) return emptyList()
            val payload = connection.inputStream.bufferedReader().use { reader -> reader.readText() }
            val sources = JSONArray(payload)
            buildList {
                for (index in 0 until sources.length()) {
                    val item = sources.getJSONObject(index)
                    val sourceId = item.optString("sourceId")
                    val displayName = item.optString("displayName")
                    if (sourceId.isNotBlank()) {
                        add(
                            NdiSource(
                                sourceId = sourceId,
                                displayName = displayName.ifBlank { sourceId },
                                lastSeenAtEpochMillis = System.currentTimeMillis(),
                            ),
                        )
                    }
                }
            }
        } finally {
            connection.disconnect()
        }
    }

    private fun announceRelaySource(sender: LocalRelaySender) {
        val body = JSONObject()
            .put("sourceId", sender.sourceId)
            .put("displayName", sender.streamName)
        postRelayJson("/announce", body)
    }

    private fun revokeRelaySource(sourceId: String) {
        val body = JSONObject().put("sourceId", sourceId)
        postRelayJson("/revoke", body)
    }

    private fun postRelayJson(path: String, body: JSONObject) {
        val connection = (URL("$RELAY_BASE_URL$path").openConnection() as HttpURLConnection).apply {
            requestMethod = "POST"
            connectTimeout = 1_000
            readTimeout = 1_500
            doOutput = true
            setRequestProperty("Content-Type", "application/json")
        }
        try {
            connection.outputStream.bufferedWriter().use { writer ->
                writer.write(body.toString())
            }
            if (connection.responseCode !in 200..299) {
                error("Relay request failed for $path with ${connection.responseCode}")
            }
        } finally {
            connection.disconnect()
        }
    }

    private external fun nativeDiscoverSourceIds(): Array<String>

    private external fun nativeDiscoverDisplayNames(): Array<String>

    private external fun nativeStartReceiver(sourceId: String)

    private external fun nativeStopReceiver()

    private external fun nativeStartSender(sourceId: String, streamName: String)

    private external fun nativeStopSender()

    private external fun nativeStartLocalScreenShareSender(streamName: String)

    private external fun nativeStopLocalScreenShareSender()
}
