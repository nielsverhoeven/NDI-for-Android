package com.ndi.sdkbridge

import android.content.Context
import android.net.wifi.WifiManager
import android.util.Log
import com.ndi.core.model.NdiDiscoveryEndpoint
import com.ndi.core.model.NdiSource
import com.ndi.core.model.ViewerVideoFrame
import java.net.HttpURLConnection
import java.net.Inet4Address
import java.net.NetworkInterface
import java.net.URL
import java.util.Collections
import java.util.UUID
import javax.jmdns.JmDNS
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
    
    fun setDiscoveryEndpoint(endpoint: NdiDiscoveryEndpoint?)
}

interface NdiViewerBridge {
    fun startReceiver(sourceId: String)

    fun stopReceiver()

    fun getLatestReceiverFrame(): ViewerVideoFrame?
}

interface NdiOutputBridge {
    suspend fun isSourceReachable(sourceId: String): Boolean

    fun startSender(sourceId: String, streamName: String)

    fun stopSender()

    fun startLocalScreenShareSender(streamName: String)

    fun stopLocalScreenShareSender()
}

object NativeNdiBridge : NdiDiscoveryBridge, NdiViewerBridge, NdiOutputBridge {

    private const val RELAY_BASE_URL = "http://localhost:17455"
    private const val HEARTBEAT_INTERVAL_MS = 1_000L
    private const val MDNS_QUERY_TIMEOUT_MS = 1_200L
    private const val MDNS_CACHE_WINDOW_MS = 3_000L
    private const val NATIVE_RECEIVER_IMPLEMENTED = true

    private val MDNS_SERVICE_TYPES = arrayOf(
        "_ndi._tcp.local.",
        "_ndi-source._tcp.local.",
        "_ndi._udp.local.",
    )

    private data class LocalRelaySender(
        val sourceId: String,
        val streamName: String,
    )

    // Keep a stable per-process host id so restart+rename updates one source instead of creating duplicates.
    private val relayHostInstanceId: String = UUID.randomUUID().toString()

    private val relayScope = CoroutineScope(SupervisorJob() + Dispatchers.IO)

    @Volatile
    private var appContext: Context? = null

    @Volatile
    private var multicastLock: WifiManager.MulticastLock? = null
    
    @Volatile
    private var discoveryEndpoint: NdiDiscoveryEndpoint? = null

    private val mdnsCacheLock = Any()

    @Volatile
    private var cachedMdnsSources: List<NdiSource> = emptyList()

    @Volatile
    private var cachedMdnsUpdatedAtEpochMillis: Long = 0L

    @Volatile
    private var activeLocalSender: LocalRelaySender? = null

    @Volatile
    private var activeLocalSenderHeartbeat = null as kotlinx.coroutines.Job?

    init {
        runCatching { System.loadLibrary("ndi_bridge") }
    }

    fun initialize(context: Context) {
        appContext = context.applicationContext
    }

    override fun setDiscoveryEndpoint(endpoint: NdiDiscoveryEndpoint?) {
        discoveryEndpoint = endpoint
        Log.d("NdiDiscovery", "Endpoint set: $endpoint")
    }

    override suspend fun discoverSources(): List<NdiSource> = withContext(Dispatchers.IO) {
        val configuredEndpoint = discoveryEndpoint
        Log.d("NdiDiscovery", "discoverSources() called with endpoint: $configuredEndpoint")
        
        val configuredServerSources = if (configuredEndpoint != null) {
            runCatching { discoverFromRemoteServer(configuredEndpoint) }
                .onSuccess { sources -> Log.d("NdiDiscovery", "Remote server returned ${sources.size} sources") }
                .onFailure { error -> Log.e("NdiDiscovery", "Remote server discovery failed: ${error.message}", error) }
                .getOrDefault(emptyList())
        } else {
            Log.d("NdiDiscovery", "No configured endpoint, skipping remote server query")
            emptyList()
        }
        
        // If a configured discovery server is available and returns sources, use those
        if (configuredServerSources.isNotEmpty()) {
            Log.d("NdiDiscovery", "Using ${configuredServerSources.size} sources from remote server")
            return@withContext configuredServerSources.distinctBy { it.sourceId }
        }
        
        val relaySources = runCatching { discoverRelaySources() }.getOrDefault(emptyList())

        val sourceIds = runCatching { nativeDiscoverSourceIds() }.getOrDefault(emptyArray())
        val displayNames = runCatching { nativeDiscoverDisplayNames() }.getOrDefault(emptyArray())
        val nativeSources = sourceIds.mapIndexed { index, sourceId ->
            NdiSource(
                sourceId = sourceId,
                displayName = displayNames.getOrNull(index) ?: sourceId,
                lastSeenAtEpochMillis = System.currentTimeMillis(),
            )
        }
        Log.d("NdiDiscovery", "Found ${relaySources.size} relay sources, ${nativeSources.size} native sources")

        // When native SDK discovery is available, prefer its canonical source identities for receiver connect.
        val mdnsSources = if (nativeSources.isEmpty()) {
            runCatching { discoverMdnsSourcesCached() }.getOrDefault(emptyList())
        } else {
            emptyList()
        }

        buildList {
            addAll(relaySources)
            addAll(nativeSources)
            addAll(mdnsSources)
        }.distinctBy { it.sourceId }
    }

    private fun discoverMdnsSourcesCached(): List<NdiSource> {
        val now = System.currentTimeMillis()
        if (now - cachedMdnsUpdatedAtEpochMillis <= MDNS_CACHE_WINDOW_MS) {
            return cachedMdnsSources
        }

        synchronized(mdnsCacheLock) {
            val refreshedNow = System.currentTimeMillis()
            if (refreshedNow - cachedMdnsUpdatedAtEpochMillis <= MDNS_CACHE_WINDOW_MS) {
                return cachedMdnsSources
            }

            val discovered = discoverMdnsSources()
            cachedMdnsSources = discovered
            cachedMdnsUpdatedAtEpochMillis = refreshedNow
            return discovered
        }
    }

    private fun discoverMdnsSources(): List<NdiSource> {
        val now = System.currentTimeMillis()
        return withMulticastLock {
            val allDiscovered = mutableListOf<NdiSource>()
            for (localAddress in discoverMulticastAddresses()) {
                val jmdns = runCatching {
                    JmDNS.create(localAddress, "ndi-android-${localAddress.hostAddress}")
                }.getOrNull() ?: continue

                try {
                    for (serviceType in MDNS_SERVICE_TYPES) {
                        val serviceInfos = runCatching {
                            jmdns.list(serviceType, MDNS_QUERY_TIMEOUT_MS)
                        }.getOrDefault(emptyArray())

                        for (serviceInfo in serviceInfos) {
                            val displayName = serviceInfo.name
                                ?.trim()
                                ?.takeIf { it.isNotEmpty() }
                                ?: serviceInfo.qualifiedName

                            val host = serviceInfo.inet4Addresses
                                .firstOrNull()
                                ?.hostAddress
                                ?: serviceInfo.server
                                    ?.trimEnd('.')
                                    ?.takeIf { it.isNotBlank() }

                            val endpointAddress = if (host != null && serviceInfo.port > 0) {
                                "$host:${serviceInfo.port}"
                            } else {
                                null
                            }

                            val sourceId = endpointAddress ?: "mdns:${serviceType}:${displayName}"

                            allDiscovered += NdiSource(
                                sourceId = sourceId,
                                displayName = displayName,
                                endpointAddress = endpointAddress,
                                lastSeenAtEpochMillis = now,
                            )
                        }
                    }
                } finally {
                    runCatching { jmdns.close() }
                }
            }

            allDiscovered
                .filter { it.sourceId.isNotBlank() }
                .distinctBy { it.sourceId }
        }
    }

    private fun discoverMulticastAddresses(): List<Inet4Address> {
        val interfaces = runCatching {
            val values = NetworkInterface.getNetworkInterfaces() ?: return emptyList()
            Collections.list(values)
        }.getOrDefault(emptyList())

        return interfaces.asSequence()
            .filter { networkInterface ->
                runCatching {
                    networkInterface.isUp &&
                        !networkInterface.isLoopback &&
                        networkInterface.supportsMulticast()
                }.getOrDefault(false)
            }
            .flatMap { networkInterface ->
                runCatching {
                    Collections.list(networkInterface.inetAddresses)
                }.getOrDefault(emptyList()).asSequence()
            }
            .filterIsInstance<Inet4Address>()
            .filterNot { it.isLoopbackAddress }
            .distinctBy { it.hostAddress }
            .toList()
    }

    private inline fun <T> withMulticastLock(block: () -> T): T {
        val wifiManager = appContext?.getSystemService(Context.WIFI_SERVICE) as? WifiManager
        val lock = wifiManager?.let {
            multicastLock ?: it.createMulticastLock("ndi-mdns-discovery").apply {
                setReferenceCounted(true)
                multicastLock = this
            }
        }
        val acquiredHere = lock != null && !lock.isHeld
        if (acquiredHere) {
            runCatching { lock.acquire() }
        }
        return try {
            block()
        } finally {
            if (acquiredHere) {
                runCatching { lock?.release() }
            }
        }
    }

    override fun startReceiver(sourceId: String) {
        if (sourceId.startsWith("relay-screen:")) {
            // Relay-backed preview is rendered in ViewerFragment from /frame endpoint.
            return
        }
        if (!NATIVE_RECEIVER_IMPLEMENTED) {
            throw UnsupportedOperationException(
                "NDI receiver pipeline is not available in this build. " +
                    "Integrate the NDI native SDK receiver implementation to play network streams.",
            )
        }
        runCatching { nativeStartReceiver(sourceId) }
    }

    override fun stopReceiver() {
        runCatching { nativeStopReceiver() }
    }

    override fun getLatestReceiverFrame(): ViewerVideoFrame? {
        val width = runCatching { nativeGetLatestReceiverFrameWidth() }.getOrDefault(0)
        val height = runCatching { nativeGetLatestReceiverFrameHeight() }.getOrDefault(0)
        if (width <= 0 || height <= 0) return null

        val pixels = runCatching { nativeGetLatestReceiverFrameArgb() }.getOrNull() ?: return null
        if (pixels.size < width * height) return null

        return ViewerVideoFrame(
            width = width,
            height = height,
            argbPixels = pixels,
        )
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

    private fun discoverFromRemoteServer(endpoint: NdiDiscoveryEndpoint): List<NdiSource> {
        val url = "http://${endpoint.host}:${endpoint.resolvedPort}/sources"
        Log.d("NdiDiscovery", "Querying remote server at: $url")
        
        val connection = (URL(url).openConnection() as HttpURLConnection).apply {
            requestMethod = "GET"
            connectTimeout = 2_000
            readTimeout = 3_000
        }
        return try {
            val responseCode = connection.responseCode
            Log.d("NdiDiscovery", "Remote server response code: $responseCode")
            
            if (responseCode !in 200..299) {
                Log.e("NdiDiscovery", "Remote server error: HTTP $responseCode")
                return emptyList()
            }
            
            val payload = connection.inputStream.bufferedReader().use { reader -> reader.readText() }
            Log.d("NdiDiscovery", "Remote server response: $payload")
            
            val sources = JSONArray(payload)
            val result = buildList {
                for (index in 0 until sources.length()) {
                    val item = sources.getJSONObject(index)
                    val sourceId = item.optString("sourceId")
                    val displayName = item.optString("displayName")
                    if (sourceId.isNotBlank()) {
                        add(
                            NdiSource(
                                sourceId = sourceId,
                                displayName = displayName.ifBlank { sourceId },
                                endpointAddress = item.optString("endpointAddress").takeIf { it.isNotBlank() },
                                lastSeenAtEpochMillis = System.currentTimeMillis(),
                            ),
                        )
                    }
                }
            }
            Log.d("NdiDiscovery", "Parsed ${result.size} sources from remote server")
            result
        } catch (e: Exception) {
            Log.e("NdiDiscovery", "Failed to query remote server: ${e.message}", e)
            throw e
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

    private external fun nativeGetLatestReceiverFrameArgb(): IntArray?

    private external fun nativeGetLatestReceiverFrameWidth(): Int

    private external fun nativeGetLatestReceiverFrameHeight(): Int

    private external fun nativeStartSender(sourceId: String, streamName: String)

    private external fun nativeStopSender()

    private external fun nativeStartLocalScreenShareSender(streamName: String)

    private external fun nativeStopLocalScreenShareSender()
}
