package com.ndi.feature.ndibrowser.data

import com.ndi.feature.ndibrowser.data.repository.DeveloperDiagnosticsLogBuffer
import com.ndi.feature.ndibrowser.data.repository.DeveloperDiagnosticsRepositoryImpl
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.test.runTest
import org.junit.Assert.*
import org.junit.Test

class DeveloperDiagnosticsRepositoryImplTest {

    @Test
    fun `observeDiscoveryDiagnostics returns non-null flow`() = runTest {
        val repo = DeveloperDiagnosticsRepositoryImpl(
            viewerRepository = null,
            outputRepository = null,
            logBuffer = DeveloperDiagnosticsLogBuffer(),
        )
        val diagnostics = repo.observeDiscoveryDiagnostics().first()
        assertNotNull(diagnostics)
        assertFalse("Developer mode off by default", diagnostics.developerModeEnabled)
        assertTrue("Server rollup should be empty initially", diagnostics.serverStatusRollup.isEmpty())
    }
}
