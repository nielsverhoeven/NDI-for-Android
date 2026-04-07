package com.ndi.feature.ndibrowser.viewer

import com.ndi.core.model.NdiOverlayMode
import com.ndi.feature.ndibrowser.data.validation.AddressValidator
import com.ndi.feature.ndibrowser.settings.OverlayDisplayState
import org.junit.Assert.assertEquals
import org.junit.Test

class ViewerDeveloperLogTest {

    private val resolver = ViewerDeveloperLogResolver(AddressValidator())

    @Test
    fun replaces_redacted_token_with_configured_address_when_developer_mode_on() {
        val state = OverlayDisplayState(
            mode = NdiOverlayMode.ACTIVE,
            streamStatus = "ACTIVE",
            sessionId = "session",
            recentLogs = listOf("Connecting to [redacted-ip]"),
            configuredAddresses = listOf("192.168.1.10"),
        )

        val resolved = resolver.resolve(state)

        assertEquals(listOf("Connecting to 192.168.1.10"), resolved?.recentLogs)
    }

    @Test
    fun uses_not_configured_when_no_valid_addresses_are_present() {
        val state = OverlayDisplayState(
            mode = NdiOverlayMode.ACTIVE,
            streamStatus = "ACTIVE",
            sessionId = "session",
            recentLogs = listOf("Connecting to [redacted-ip]"),
            configuredAddresses = listOf("bad host name"),
        )

        val resolved = resolver.resolve(state)

        assertEquals(listOf("Connecting to not configured"), resolved?.recentLogs)
    }

    @Test
    fun does_not_transform_logs_when_overlay_mode_is_disabled() {
        val state = OverlayDisplayState(
            mode = NdiOverlayMode.DISABLED,
            streamStatus = null,
            sessionId = null,
            recentLogs = listOf("Connecting to [redacted-ip]"),
            configuredAddresses = listOf("192.168.1.10"),
        )

        val resolved = resolver.resolve(state)

        assertEquals(listOf("Connecting to [redacted-ip]"), resolved?.recentLogs)
    }

    @Test
    fun leaves_log_untouched_when_token_is_missing() {
        val state = OverlayDisplayState(
            mode = NdiOverlayMode.ACTIVE,
            streamStatus = "ACTIVE",
            sessionId = "session",
            recentLogs = listOf("Stream connected"),
            configuredAddresses = listOf("192.168.1.10"),
        )

        val resolved = resolver.resolve(state)

        assertEquals(listOf("Stream connected"), resolved?.recentLogs)
    }

    @Test
    fun replaces_redacted_token_when_port_suffix_is_present() {
        val state = OverlayDisplayState(
            mode = NdiOverlayMode.ACTIVE,
            streamStatus = "ACTIVE",
            sessionId = "session",
            recentLogs = listOf("Connecting to [redacted-ip]:5959"),
            configuredAddresses = listOf("192.168.1.10"),
        )

        val resolved = resolver.resolve(state)

        assertEquals(listOf("Connecting to 192.168.1.10:5959"), resolved?.recentLogs)
    }

    @Test
    fun replaces_legacy_redacted_token_variant() {
        val state = OverlayDisplayState(
            mode = NdiOverlayMode.ACTIVE,
            streamStatus = "ACTIVE",
            sessionId = "session",
            recentLogs = listOf("Connecting to [REDACTED]:5959"),
            configuredAddresses = listOf("192.168.1.10"),
        )

        val resolved = resolver.resolve(state)

        assertEquals(listOf("Connecting to 192.168.1.10:5959"), resolved?.recentLogs)
    }
}
