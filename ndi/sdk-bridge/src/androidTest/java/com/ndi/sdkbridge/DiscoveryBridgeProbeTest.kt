package com.ndi.sdkbridge

import android.util.Log
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import com.ndi.core.model.NdiDiscoveryEndpoint
import kotlinx.coroutines.delay
import kotlinx.coroutines.runBlocking
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class DiscoveryBridgeProbeTest {

    @Test
    fun probeDiscoveryServerSources() {
        runBlocking {
            val context = InstrumentationRegistry.getInstrumentation().targetContext
            NativeNdiBridge.initialize(context)
            NativeNdiBridge.setDiscoveryEndpoints(
                listOf(
                    NdiDiscoveryEndpoint(
                        host = "10.10.0.53",
                        port = 5959,
                        resolvedPort = 5959,
                        usesDefaultPort = false,
                    ),
                ),
            )

            val discovered = linkedMapOf<String, String>()
            repeat(8) {
                NativeNdiBridge.discoverSources().forEach { source ->
                    val key = source.displayName.ifBlank { source.sourceId }
                    val value = source.endpointAddress ?: source.sourceId
                    discovered[key] = value
                }
                delay(1_000)
            }

            val entries = discovered.entries.joinToString(separator = "|") { (name, endpoint) ->
                "$name@$endpoint"
            }
            Log.i("NdiProbe", "NDI_PROBE_RESULT count=${discovered.size} entries=$entries")
        }
    }
}
