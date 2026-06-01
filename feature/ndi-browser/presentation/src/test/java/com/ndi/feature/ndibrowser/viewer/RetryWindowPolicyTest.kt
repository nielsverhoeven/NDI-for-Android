package com.ndi.feature.ndibrowser.viewer

import com.ndi.feature.ndibrowser.data.ViewerReconnectCoordinator
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class RetryWindowPolicyTest {

    @Test
    fun retryWithinWindow_stopsAfterConfiguredAttempts() = runTest {
        val coordinator = ViewerReconnectCoordinator(retryDelayMillis = 0)
        var attempts = 0

        val result = coordinator.retryWithinWindow(windowSeconds = 3) {
            attempts += 1
            false
        }

        assertFalse(result.recovered)
        assertEquals(3, attempts)
    }

    @Test
    fun retryWithinWindow_returnsRecoveredWhenAttemptSucceeds() = runTest {
        val coordinator = ViewerReconnectCoordinator(retryDelayMillis = 0)
        var attempts = 0

        val result = coordinator.retryWithinWindow(windowSeconds = 5) {
            attempts += 1
            attempts == 2
        }

        assertTrue(result.recovered)
        assertEquals(2, result.attempts)
    }
}
