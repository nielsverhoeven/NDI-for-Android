package com.ndi.feature.ndibrowser.data

import com.ndi.core.model.DiscoveryCheckOutcome
import com.ndi.core.model.DiscoveryCheckType
import com.ndi.core.model.DiscoveryFailureCategory
import com.ndi.core.model.DiscoveryServerCheckStatus
import kotlinx.coroutines.test.runTest
import org.junit.Assert.*
import org.junit.Test

class DiscoveryProtocolCheckTest {

    @Test
    fun `check result SUCCESS outcome has NONE failure category and null message`() = runTest {
        val status = DiscoveryServerCheckStatus(
            serverId = "server-1",
            checkType = DiscoveryCheckType.ADD_VALIDATION,
            outcome = DiscoveryCheckOutcome.SUCCESS,
            checkedAtEpochMillis = System.currentTimeMillis(),
            failureCategory = DiscoveryFailureCategory.NONE,
            failureMessage = null,
            correlationId = "corr-1",
        )
        assertEquals(DiscoveryCheckOutcome.SUCCESS, status.outcome)
        assertEquals(DiscoveryFailureCategory.NONE, status.failureCategory)
        assertNull(status.failureMessage)
    }

    @Test
    fun `check result FAILURE outcome has non-NONE failure category and non-null message`() = runTest {
        val status = DiscoveryServerCheckStatus(
            serverId = "server-1",
            checkType = DiscoveryCheckType.ADD_VALIDATION,
            outcome = DiscoveryCheckOutcome.FAILURE,
            checkedAtEpochMillis = System.currentTimeMillis(),
            failureCategory = DiscoveryFailureCategory.TIMEOUT,
            failureMessage = "Connection timed out",
            correlationId = "corr-1",
        )
        assertEquals(DiscoveryCheckOutcome.FAILURE, status.outcome)
        assertNotEquals(DiscoveryFailureCategory.NONE, status.failureCategory)
        assertNotNull(status.failureMessage)
    }
}
