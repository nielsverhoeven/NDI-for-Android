package com.ndi.sdkbridge

import android.content.Context
import android.content.Intent
import android.net.wifi.WifiManager
import android.util.Log
import com.ndi.core.model.NdiDiscoveryEndpoint
import com.ndi.core.model.NdiSource
import com.ndi.core.model.ViewerVideoFrame
import java.net.HttpURLConnection
import java.net.InetAddress
import java.net.Inet4Address
import java.net.InetSocketAddress
import java.net.NetworkInterface
import java.net.Socket
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

    fun applyReceiverQualityProfile(profileId: String, maxWidth: Int, maxHeight: Int, targetFps: Int) {}

    fun setFrameRatePolicy(targetFps: Int): Boolean = false

    fun setResolutionPolicy(width: Int, height: Int): Boolean = false

    fun getReceiverDroppedFramePercent(): Float = 0f

    fun getActualResolution(): Pair<Int, Int> = 0 to 0

    fun getMeasuredReceiverFps(): Float = 0f
}

interface NdiOutputBridge {
    suspend fun isSourceReachable(sourceId: String): Boolean

    suspend fun isDiscoveryServerReachable(host: String, port: Int?): Boolean

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
    private var pendingScreenCaptureTokenRef: String? = null

    @Volatile
    private var activeLocalSenderHeartbeat = null as kotlinx.coroutines.Job?

    init {
        runCatching { System.loadLibrary("ndi_bridge") }
    }

    fun initialize(context: Context) {
        appContext = context.applicationContext
    }

    fun registerScreenCapturePermissionResult(resultCode: Int, data: Intent?): String? {
        return ScreenCapturePermissionStore.register(resultCode, data)
    }

    fun setPendingLocalScreenShareToken(tokenRef: String?) {
        pendingScreenCaptureTokenRef = tokenRef
    }

    override fun setDiscoveryEndpoint(endpoint: NdiDiscoveryEndpoint?) {
        discoveryEndpoint = endpoint
        val extraIps = endpoint?.host
            ?.takeIf { it.isNotBlank() }
            ?.let(::resolveDiscoveryExtraIps)
        nativeSetDiscoveryExtraIps(extraIps)
        Log.d("NdiDiscovery", "Endpoint set: $endpoint, extraIps=$extraIps")
    }

    override suspend fun discoverSources(): List<NdiSource> = withContext(Dispatchers.IO) {
        val configuredEndpoint = discoveryEndpoint
        Log.d("NdiDiscovery", "discoverSources() called with endpoint: $configuredEndpoint")

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

    private fun resolveDiscoveryExtraIps(host: String): String {
        val resolved = runCatching { InetAddress.getByName(host).hostAddress }
            .getOrElse {
                Log.w("NdiDiscovery", "Falling back to raw host for discovery extra IPs: $host", it)
                host
            }
        return resolved.trim('[', ']')
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

    override fun applyReceiverQualityProfile(profileId: String, maxWidth: Int, maxHeight: Int, targetFps: Int) {
        runCatching {
            nativeApplyReceiverQualityProfile(profileId, maxWidth, maxHeight, targetFps)
        }
    }

    override fun setFrameRatePolicy(targetFps: Int): Boolean {
        return runCatching { nativeSetFrameRatePolicy(targetFps) }.getOrDefault(false)
    }

    override fun setResolutionPolicy(width: Int, height: Int): Boolean {
        return runCatching { nativeSetResolutionPolicy(width, height) }.getOrDefault(false)
    }

    override fun getReceiverDroppedFramePercent(): Float {
        return runCatching { nativeGetReceiverDroppedFramePercent() }.getOrDefault(0f).coerceIn(0f, 100f)
    }

    override fun getActualResolution(): Pair<Int, Int> {
        val values = runCatching { nativeGetActualResolution() }.getOrNull() ?: return 0 to 0
        if (values.size < 2) return 0 to 0
        return values[0].coerceAtLeast(0) to values[1].coerceAtLeast(0)
    }

    override fun getMeasuredReceiverFps(): Float {
        return runCatching { nativeGetMeasuredReceiverFps() }.getOrDefault(0f).coerceAtLeast(0f)
    }

    override suspend fun isSourceReachable(sourceId: String): Boolean = withContext(Dispatchers.IO) {
        discoverSources().any { it.sourceId == sourceId }
    }

    override suspend fun isDiscoveryServerReachable(host: String, port: Int?): Boolean = withContext(Dispatchers.IO) {
        val targetHost = host.trim()
        if (targetHost.isBlank()) return@withContext false

        val targetPort = port ?: 5960
        runCatching {
            Socket().use { socket ->
                socket.connect(InetSocketAddress(targetHost, targetPort), 1500)
            }
            true
        }.getOrDefault(false)
    }

    override fun startSender(sourceId: String, streamName: String) {
        nativeStartSender(sourceId, streamName)
    }

    override fun stopSender() {
        runCatching { nativeStopSender() }
    }

    override fun startLocalScreenShareSender(streamName: String) {
        val normalizedName = streamName.trim().ifBlank { "NDI Output" }
        val context = requireNotNull(appContext) { "App context is not initialized" }
        val tokenRef = pendingScreenCaptureTokenRef
        val permissionGrant = ScreenCapturePermissionStore.get(tokenRef)
            ?: error("Screen capture permission is not available")
        val session = LocalRelaySender(
            sourceId = "relay-screen:$relayHostInstanceId",
            streamName = normalizedName,
        )

        stopLocalScreenShareSender()

        activeLocalSenderHeartbeat?.cancel()
        activeLocalSender = session
        activeLocalSenderHeartbeat = relayScope.launch {
            while (isActive) {
                runCatching { announceRelaySource(session) }
                delay(HEARTBEAT_INTERVAL_MS)
            }
        }

        try {
            ScreenShareController.start(context, permissionGrant, normalizedName) { width, height, pixels ->
                nativeSubmitLocalScreenShareFrameArgb(width, height, pixels)
            }
            nativeStartLocalScreenShareSender(normalizedName)
        } catch (error: Throwable) {
            Log.e("NdiDiscovery", "Unable to start local screen share sender", error)
            runCatching { ScreenShareController.stop() }
            runCatching { nativeStopLocalScreenShareSender() }
            val activeSession = activeLocalSender
            activeLocalSender = null
            activeLocalSenderHeartbeat?.cancel()
            activeLocalSenderHeartbeat = null
            if (activeSession != null) {
                runCatching { revokeRelaySource(activeSession.sourceId) }
            }
            throw error
        }
    }

    override fun stopLocalScreenShareSender() {
        val session = activeLocalSender
        activeLocalSender = null
        activeLocalSenderHeartbeat?.cancel()
        activeLocalSenderHeartbeat = null
        runCatching { ScreenShareController.stop() }
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

    private external fun nativeSetDiscoveryExtraIps(extraIpsCsv: String?)

    private external fun nativeStartReceiver(sourceId: String)

    private external fun nativeStopReceiver()

    private external fun nativeGetLatestReceiverFrameArgb(): IntArray?

    private external fun nativeGetLatestReceiverFrameWidth(): Int

    private external fun nativeGetLatestReceiverFrameHeight(): Int

    private external fun nativeApplyReceiverQualityProfile(
        profileId: String,
        maxWidth: Int,
        maxHeight: Int,
        targetFps: Int,
    )

    private external fun nativeGetReceiverDroppedFramePercent(): Float

    private external fun nativeSetFrameRatePolicy(targetFps: Int): Boolean

    private external fun nativeSetResolutionPolicy(width: Int, height: Int): Boolean

    private external fun nativeGetActualResolution(): IntArray?

    private external fun nativeGetMeasuredReceiverFps(): Float

    private external fun nativeStartSender(sourceId: String, streamName: String)

    private external fun nativeStopSender()

    private external fun nativeStartLocalScreenShareSender(streamName: String)

    private external fun nativeSubmitLocalScreenShareFrameArgb(width: Int, height: Int, argbPixels: IntArray)

    private external fun nativeStopLocalScreenShareSender()
}
