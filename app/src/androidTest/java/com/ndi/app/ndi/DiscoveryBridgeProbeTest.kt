package com.ndi.app.ndi

import android.util.Log
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import com.ndi.core.model.NdiDiscoveryEndpoint
import com.ndi.sdkbridge.NativeNdiBridge
import kotlinx.coroutines.delay
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withTimeoutOrNull
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class DiscoveryBridgeProbeTest {

    @Test
    fun probeDiscoveryServerSources() {
        runBlocking {
            val args = InstrumentationRegistry.getArguments()
            val discoveryHost = args.getString("discoveryHost")?.trim().orEmpty().ifBlank { "10.10.0.53" }
            val discoveryPort = args.getString("discoveryPort")?.toIntOrNull() ?: 5959

            val context = InstrumentationRegistry.getInstrumentation().targetContext
            NativeNdiBridge.initialize(context)
            NativeNdiBridge.setDiscoveryEndpoints(
                listOf(
                    NdiDiscoveryEndpoint(
                        host = discoveryHost,
                        port = discoveryPort,
                        resolvedPort = discoveryPort,
                        usesDefaultPort = discoveryPort == 5959,
                    ),
                ),
            )

            val discovered = linkedMapOf<String, String>()
            repeat(6) {
                val sources = withTimeoutOrNull(6_000) { NativeNdiBridge.discoverSources() } ?: emptyList()
                sources.forEach { source ->
                    val key = source.displayName.ifBlank { source.sourceId }
                    val value = source.endpointAddress ?: source.sourceId
                    discovered[key] = value
                }
                delay(1_500)
            }

            val entries = discovered.entries.joinToString(separator = "|") { (name, endpoint) ->
                "$name@$endpoint"
            }
            Log.i("NdiProbe", "NDI_PROBE_RESULT count=${discovered.size} entries=$entries")
        }
    }
}
