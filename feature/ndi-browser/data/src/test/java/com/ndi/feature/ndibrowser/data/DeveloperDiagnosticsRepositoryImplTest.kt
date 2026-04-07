package com.ndi.feature.ndibrowser.data

import com.ndi.core.model.DiscoveryCompatibilityResult
import com.ndi.core.model.DiscoveryCompatibilityStatus
import com.ndi.feature.ndibrowser.data.repository.DeveloperDiagnosticsLogBuffer
import com.ndi.feature.ndibrowser.data.repository.DiscoveryCompatibilityMatrixRepositoryImpl
import com.ndi.feature.ndibrowser.data.repository.DeveloperDiagnosticsRepositoryImpl
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotNull
import org.junit.Assert.assertTrue
import org.junit.Test

class DeveloperDiagnosticsRepositoryImplTest {

    @Test
    fun `observeDiscoveryDiagnostics returns non-null flow`() = runTest {
        val matrixRepository = DiscoveryCompatibilityMatrixRepositoryImpl()
        val repo = DeveloperDiagnosticsRepositoryImpl(
            viewerRepository = null,
            outputRepository = null,
            logBuffer = DeveloperDiagnosticsLogBuffer(),
            compatibilityMatrixRepository = matrixRepository,
        )
        val diagnostics = repo.observeDiscoveryDiagnostics().first()
        assertNotNull(diagnostics)
        assertFalse("Developer mode off by default", diagnostics.developerModeEnabled)
        assertTrue("Server rollup should be empty initially", diagnostics.serverStatusRollup.isEmpty())
    }

    @Test
    fun `observeDiscoveryDiagnostics includes compatibility matrix summary and actionable lines`() = runTest {
        val matrixRepository = DiscoveryCompatibilityMatrixRepositoryImpl()
        matrixRepository.upsertResults(
            listOf(
                compatibilityResult("baseline", DiscoveryCompatibilityStatus.COMPATIBLE),
                compatibilityResult("venue", DiscoveryCompatibilityStatus.BLOCKED),
                compatibilityResult("older", DiscoveryCompatibilityStatus.INCOMPATIBLE),
            ),
            recordedAtEpochMillis = 123L,
        )
        val repo = DeveloperDiagnosticsRepositoryImpl(
            viewerRepository = null,
            outputRepository = null,
            logBuffer = DeveloperDiagnosticsLogBuffer(),
            compatibilityMatrixRepository = matrixRepository,
        )

        val diagnostics = repo.observeDiscoveryDiagnostics().first {
            it.recentDiscoveryLogs.any { line -> line.contains("compatibility matrix:") }
        }

        assertTrue(
            diagnostics.recentDiscoveryLogs.any { line ->
                line.contains("compatible=1") &&
                    line.contains("incompatible=1") &&
                    line.contains("blocked=1")
            },
        )
        assertTrue(
            diagnostics.recentDiscoveryLogs.any { line ->
                line.contains("target=venue") &&
                    line.contains("blocked") &&
                    line.contains("next=")
            },
        )
        assertTrue(
            diagnostics.recentDiscoveryLogs.any { line ->
                line.contains("target=older") &&
                    line.contains("incompatible") &&
                    line.contains("next=")
            },
        )
    }

    private fun compatibilityResult(
        targetId: String,
        status: DiscoveryCompatibilityStatus,
    ): DiscoveryCompatibilityResult {
        return DiscoveryCompatibilityResult(
            targetId = targetId,
            status = status,
            discoveredSourceCount = 0,
            streamStartAttempted = false,
            streamStartSucceeded = false,
            notes = null,
        )
    }
}
