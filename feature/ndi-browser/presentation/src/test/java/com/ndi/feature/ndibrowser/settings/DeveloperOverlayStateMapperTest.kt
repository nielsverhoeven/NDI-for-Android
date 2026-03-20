package com.ndi.feature.ndibrowser.settings

import com.ndi.core.model.NdiOverlayMode
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
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
        )

        assertNull(state.streamStatus)
    }
}