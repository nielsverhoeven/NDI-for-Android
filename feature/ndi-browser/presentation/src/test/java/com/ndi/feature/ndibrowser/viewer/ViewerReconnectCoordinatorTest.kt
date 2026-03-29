package com.ndi.feature.ndibrowser.viewer

import com.ndi.feature.ndibrowser.data.ViewerReconnectCoordinator
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class ViewerReconnectCoordinatorTest {

    @Test
    fun retryWindow_usesWindowSecondsAsAttemptBudget() = runTest {
        val coordinator = ViewerReconnectCoordinator(retryDelayMillis = 0)
        var attempts = 0

        val result = coordinator.retryWithinWindow(windowSeconds = 4) {
            attempts += 1
            false
        }

        assertFalse(result.recovered)
        assertEquals(4, result.attempts)
        assertEquals(4, attempts)
    }

    @Test
    fun retryWindow_returnsRecoveredWhenAttemptEventuallySucceeds() = runTest {
        val coordinator = ViewerReconnectCoordinator(retryDelayMillis = 0)
        var attempts = 0

        val result = coordinator.retryWithinWindow(windowSeconds = 5) {
            attempts += 1
            attempts == 3
        }

        assertTrue(result.recovered)
        assertEquals(3, result.attempts)
    }

    @Test
    fun retryWindow_stopsImmediatelyOnFirstSuccess() = runTest {
        val coordinator = ViewerReconnectCoordinator(retryDelayMillis = 0)
        var attempts = 0

        val result = coordinator.retryWithinWindow(windowSeconds = 5) {
            attempts += 1
            true
        }

        assertTrue(result.recovered)
        assertEquals(1, result.attempts)
        assertEquals(1, attempts)
    }
}
