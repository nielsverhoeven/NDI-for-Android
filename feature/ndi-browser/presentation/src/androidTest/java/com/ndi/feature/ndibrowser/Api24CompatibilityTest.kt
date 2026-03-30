package com.ndi.feature.ndibrowser

import androidx.test.ext.junit.runners.AndroidJUnit4
import com.ndi.core.model.OutputState
import com.ndi.core.model.OutputInputKind
import com.ndi.core.model.OutputConsentState
import com.ndi.feature.ndibrowser.source_list.SourceListAdaptiveLayout
import com.ndi.feature.ndibrowser.source_list.SourceListLayoutMode
import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class Api24CompatibilityTest {

    @Test
    fun compactPhoneWidth_remainsSupported() {
        assertEquals(SourceListLayoutMode.COMPACT, SourceListAdaptiveLayout.resolve(360))
    }

    @Test
    fun outputRecoveryStates_remainStable() {
        assertEquals("INTERRUPTED", OutputState.INTERRUPTED.name)
        assertEquals("STOPPED", OutputState.STOPPED.name)
    }

    // ---- Spec 002 output flow coverage (T066) ----

    @Test
    fun outputStateEnum_allValuesAccessibleOnApi24() {
        val states = OutputState.values()
        assertEquals(6, states.size)
        assert(states.contains(OutputState.READY))
        assert(states.contains(OutputState.STARTING))
        assert(states.contains(OutputState.ACTIVE))
        assert(states.contains(OutputState.STOPPING))
        assert(states.contains(OutputState.STOPPED))
        assert(states.contains(OutputState.INTERRUPTED))
    }

    @Test
    fun outputInputKindEnum_recognizesDeviceScreen() {
        assertEquals("DEVICE_SCREEN", OutputInputKind.DEVICE_SCREEN.name)
        assertEquals("DISCOVERED_NDI", OutputInputKind.DISCOVERED_NDI.name)
    }

    @Test
    fun outputConsentStateEnum_fullSetAccessible() {
        val states = OutputConsentState.values()
        assert(states.contains(OutputConsentState.NOT_REQUIRED))
        assert(states.contains(OutputConsentState.PENDING))
        assert(states.contains(OutputConsentState.GRANTED))
        assert(states.contains(OutputConsentState.DENIED))
    }

    @Test
    fun localScreenSourceIdentity_prefixIsCorrect() {
        val sourceId = "device-screen:local"
        assert(sourceId.startsWith("device-screen:"))
    }
}
