package com.ndi.feature.ndibrowser.data

import kotlinx.coroutines.cancel
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.TestScope
import kotlinx.coroutines.test.advanceTimeBy
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class DiscoveryRefreshPolicyTest {

    @Test
    fun start_runsImmediatelyAndOnInterval() = runTest {
        val dispatcher = StandardTestDispatcher(testScheduler)
        val scope = TestScope(dispatcher)
        val coordinator = DiscoveryRefreshCoordinator(scope)
        var invocationCount = 0

        try {
            coordinator.start(intervalSeconds = 5) {
                invocationCount += 1
            }

            dispatcher.scheduler.runCurrent()
            assertEquals(1, invocationCount)

            advanceTimeBy(5000)
            dispatcher.scheduler.runCurrent()
            assertEquals(2, invocationCount)
        } finally {
            coordinator.stop()
            scope.cancel()
        }
    }

    @Test
    fun stop_preventsFutureInvocations() = runTest {
        val dispatcher = StandardTestDispatcher(testScheduler)
        val scope = TestScope(dispatcher)
        val coordinator = DiscoveryRefreshCoordinator(scope)
        var invocationCount = 0

        try {
            coordinator.start(intervalSeconds = 5) {
                invocationCount += 1
            }
            dispatcher.scheduler.runCurrent()

            coordinator.stop()
            advanceTimeBy(10000)
            dispatcher.scheduler.runCurrent()

            assertEquals(1, invocationCount)
        } finally {
            scope.cancel()
        }
    }
}
