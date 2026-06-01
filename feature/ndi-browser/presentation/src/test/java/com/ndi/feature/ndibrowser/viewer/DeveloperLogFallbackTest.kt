package com.ndi.feature.ndibrowser.viewer

import com.ndi.feature.ndibrowser.data.logging.DeveloperLogFallback
import org.junit.Assert.assertEquals
import org.junit.Test

class DeveloperLogFallbackTest {

    @Test
    fun returns_not_configured_fallback_for_empty_reason() {
        assertEquals(
            "not configured",
            DeveloperLogFallback.getFallbackMessage(""),
        )
    }

    @Test
    fun returns_not_configured_fallback_for_any_reason() {
        assertEquals(
            "not configured",
            DeveloperLogFallback.getFallbackMessage("no_valid_addresses"),
        )
    }
}
