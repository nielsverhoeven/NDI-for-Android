package com.ndi.feature.ndibrowser.data

import com.ndi.core.model.DiscoveryCompatibilityResult
import com.ndi.core.model.DiscoveryCompatibilityStatus
import com.ndi.feature.ndibrowser.data.repository.DiscoveryCompatibilityMatrixRepositoryImpl
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class DiscoveryCompatibilityMatrixRepositoryTest {

    @Test
    fun upsertResults_persistsFinalStatusTaxonomyPerTarget() = runTest {
        val repository = DiscoveryCompatibilityMatrixRepositoryImpl()

        repository.upsertResults(
            results = listOf(
                result("baseline", DiscoveryCompatibilityStatus.COMPATIBLE),
                result("older-1", DiscoveryCompatibilityStatus.LIMITED),
                result("older-2", DiscoveryCompatibilityStatus.INCOMPATIBLE),
                result("older-3", DiscoveryCompatibilityStatus.BLOCKED),
                result("pending-x", DiscoveryCompatibilityStatus.PENDING),
            ),
            recordedAtEpochMillis = 100L,
        )

        val snapshot = repository.getCurrentMatrix()

        assertEquals(4, snapshot.results.size)
        assertTrue(snapshot.results.any { it.targetId == "baseline" && it.status == DiscoveryCompatibilityStatus.COMPATIBLE })
        assertTrue(snapshot.results.any { it.targetId == "older-1" && it.status == DiscoveryCompatibilityStatus.LIMITED })
        assertTrue(snapshot.results.any { it.targetId == "older-2" && it.status == DiscoveryCompatibilityStatus.INCOMPATIBLE })
        assertTrue(snapshot.results.any { it.targetId == "older-3" && it.status == DiscoveryCompatibilityStatus.BLOCKED })
        assertFalse(snapshot.results.any { it.status == DiscoveryCompatibilityStatus.PENDING })
    }

    @Test
    fun upsertResults_latestWriteWinsForSameTarget() = runTest {
        val repository = DiscoveryCompatibilityMatrixRepositoryImpl()

        repository.upsertResults(
            results = listOf(result("venue", DiscoveryCompatibilityStatus.BLOCKED)),
            recordedAtEpochMillis = 100L,
        )
        repository.upsertResults(
            results = listOf(result("venue", DiscoveryCompatibilityStatus.LIMITED)),
            recordedAtEpochMillis = 200L,
        )

        val snapshot = repository.getCurrentMatrix()

        assertEquals(1, snapshot.results.size)
        assertEquals(DiscoveryCompatibilityStatus.LIMITED, snapshot.results.single().status)
        assertEquals(200L, snapshot.recordedAtEpochMillis)
    }

    private fun result(
        targetId: String,
        status: DiscoveryCompatibilityStatus,
    ): DiscoveryCompatibilityResult {
        return DiscoveryCompatibilityResult(
            targetId = targetId,
            status = status,
            discoveredSourceCount = 0,
            streamStartAttempted = false,
            streamStartSucceeded = false,
            temporaryUnknownObserved = status == DiscoveryCompatibilityStatus.PENDING,
            notes = null,
        )
    }
}
