package com.ndi.feature.ndibrowser.settings

import com.ndi.core.model.CompatibilityGuidance
import com.ndi.core.model.DiscoveryCompatibilityStatus
import com.ndi.core.model.DeveloperDiscoveryDiagnostics
import com.ndi.core.model.DiscoveryCheckOutcome
import com.ndi.core.model.DiscoveryCheckType
import com.ndi.core.model.DiscoveryFailureCategory
import com.ndi.core.model.DiscoveryStatus
import com.ndi.core.model.DiscoveryServerCheckStatus
import com.ndi.core.model.NdiOverlayMode
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.junit.runners.JUnit4

@RunWith(JUnit4::class)
class DeveloperOverlayStateMapperTest {

    @Test
    fun developerModeOff_mapsToDisabled() {
        val state = DeveloperOverlayStateMapper.map(
            developerModeEnabled = false,
            streamStatus = "PLAYING",
            sessionId = "session-1234",
            recentLogs = listOf("viewer started"),
        )

        assertEquals(NdiOverlayMode.DISABLED, state.mode)
    }

    @Test
    fun developerModeOn_withoutActiveStream_mapsToIdle() {
        val state = DeveloperOverlayStateMapper.map(
            developerModeEnabled = true,
            streamStatus = null,
            sessionId = null,
            recentLogs = emptyList(),
        )

        assertEquals(NdiOverlayMode.IDLE, state.mode)
    }

    @Test
    fun developerModeOn_withActiveStream_mapsToActive() {
        val state = DeveloperOverlayStateMapper.map(
            developerModeEnabled = true,
            streamStatus = "ACTIVE",
            sessionId = "session-1234",
            recentLogs = listOf("stream active"),
        )

        assertEquals(NdiOverlayMode.ACTIVE, state.mode)
    }

    @Test
    fun activeState_exposesStreamStatusAndSessionId() {
        val state = DeveloperOverlayStateMapper.map(
            developerModeEnabled = true,
            streamStatus = "CONNECTING",
            sessionId = "session-1234",
            recentLogs = listOf("connecting"),
        )

        assertEquals("CONNECTING", state.streamStatus)
        assertEquals("session-1234", state.sessionId)
        assertEquals(listOf("connecting"), state.recentLogs)
    }

    @Test
    fun disabledState_clearsStreamStatus() {
        val state = DeveloperOverlayStateMapper.map(
            developerModeEnabled = false,
            streamStatus = "ACTIVE",
            sessionId = "session-1234",
            recentLogs = listOf("stream active"),
            configuredAddresses = listOf("192.168.1.10"),
            discoveryDiagnostics = sampleDiagnostics(),
        )

        assertNull(state.streamStatus)
        assertNull(state.discoveryDiagnostics)
        assertEquals(emptyList<String>(), state.configuredAddresses)
    }

    @Test
    fun enabledState_keepsDiscoveryDiagnostics() {
        val state = DeveloperOverlayStateMapper.map(
            developerModeEnabled = true,
            streamStatus = "ACTIVE",
            sessionId = "session-1234",
            recentLogs = listOf("stream active"),
            configuredAddresses = listOf("192.168.1.10", "ff02::1"),
            discoveryDiagnostics = sampleDiagnostics(),
        )

        assertNotNull(state.discoveryDiagnostics)
        assertEquals(1, state.discoveryDiagnostics?.serverStatusRollup?.size)
        assertEquals(listOf("192.168.1.10", "ff02::1"), state.configuredAddresses)
    }

    @Test
    fun enabledState_mapsCompatibilityGuidanceToOverlayMessages() {
        val state = DeveloperOverlayStateMapper.map(
            developerModeEnabled = true,
            streamStatus = "ACTIVE",
            sessionId = "session-1234",
            recentLogs = listOf("stream active"),
            discoveryDiagnostics = sampleDiagnostics(),
        )

        assertEquals(1, state.compatibilityMessages.size)
        assertTrue(state.compatibilityMessages.first().contains("server-compat"))
        assertTrue(state.compatibilityMessages.first().contains("blocked"))
    }

    @Test
    fun disabledState_clearsCompatibilityMessages() {
        val state = DeveloperOverlayStateMapper.map(
            developerModeEnabled = false,
            streamStatus = "ACTIVE",
            sessionId = "session-1234",
            recentLogs = listOf("stream active"),
            discoveryDiagnostics = sampleDiagnostics(),
        )

        assertTrue(state.compatibilityMessages.isEmpty())
    }

    private fun sampleDiagnostics(): DeveloperDiscoveryDiagnostics =
        DeveloperDiscoveryDiagnostics(
            developerModeEnabled = true,
            latestDiscoveryRefreshStatus = DiscoveryStatus.SUCCESS,
            latestDiscoveryRefreshAtEpochMillis = 1234L,
            serverStatusRollup = listOf(
                DiscoveryServerCheckStatus(
                    serverId = "server-1",
                    checkType = DiscoveryCheckType.ADD_VALIDATION,
                    outcome = DiscoveryCheckOutcome.SUCCESS,
                    checkedAtEpochMillis = 1234L,
                    failureCategory = DiscoveryFailureCategory.NONE,
                    failureMessage = null,
                    correlationId = "corr-1",
                ),
            ),
            recentDiscoveryLogs = listOf("redacted-log"),
            compatibilityGuidance = listOf(
                CompatibilityGuidance(
                    targetId = "server-compat",
                    status = DiscoveryCompatibilityStatus.BLOCKED,
                    message = "Target is blocked",
                    recommendedNextStep = "verify endpoint",
                ),
            ),
        )
    }