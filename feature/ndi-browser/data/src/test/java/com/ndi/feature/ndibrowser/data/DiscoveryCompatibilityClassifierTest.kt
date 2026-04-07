package com.ndi.feature.ndibrowser.data

import com.ndi.core.model.DiscoveryCompatibilityStatus
import com.ndi.feature.ndibrowser.data.repository.DiscoveryCompatibilityClassifier
import org.junit.Assert.assertEquals
import org.junit.Test

class DiscoveryCompatibilityClassifierTest {

    private val classifier = DiscoveryCompatibilityClassifier()

    @Test
    fun classify_returnsBlocked_whenBlocked() {
        val result = classifier.classify(
            discoverySucceeded = true,
            streamStartAttempted = true,
            streamStartSucceeded = true,
            blocked = true,
        )

        assertEquals(DiscoveryCompatibilityStatus.BLOCKED, result)
    }

    @Test
    fun classify_returnsCompatible_whenDiscoveryAndStreamStartSucceed() {
        val result = classifier.classify(
            discoverySucceeded = true,
            streamStartAttempted = true,
            streamStartSucceeded = true,
            blocked = false,
        )

        assertEquals(DiscoveryCompatibilityStatus.COMPATIBLE, result)
    }

    @Test
    fun classify_returnsIncompatible_whenDiscoverySucceedsAndStreamStartFails() {
        val result = classifier.classify(
            discoverySucceeded = true,
            streamStartAttempted = true,
            streamStartSucceeded = false,
            blocked = false,
        )

        assertEquals(DiscoveryCompatibilityStatus.INCOMPATIBLE, result)
    }

    @Test
    fun classify_returnsLimited_whenDiscoverySucceedsWithoutStreamStartAttempt() {
        val result = classifier.classify(
            discoverySucceeded = true,
            streamStartAttempted = false,
            streamStartSucceeded = false,
            blocked = false,
        )

        assertEquals(DiscoveryCompatibilityStatus.LIMITED, result)
    }

    @Test
    fun classify_returnsIncompatible_whenAnyStreamStartAttemptFailsAfterDiscovery() {
        val result = classifier.classify(
            discoverySucceeded = true,
            streamStartAttempted = true,
            streamStartSucceeded = false,
            blocked = false,
        )

        assertEquals(DiscoveryCompatibilityStatus.INCOMPATIBLE, result)
    }

    @Test
    fun classify_returnsPending_whenDiscoveryDoesNotSucceed() {
        val result = classifier.classify(
            discoverySucceeded = false,
            streamStartAttempted = false,
            streamStartSucceeded = false,
            blocked = false,
        )

        assertEquals(DiscoveryCompatibilityStatus.PENDING, result)
    }
}
