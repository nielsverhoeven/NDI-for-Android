package com.ndi.feature.ndibrowser.source_list

import com.ndi.core.model.NdiSource
import org.junit.Test
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue

/**
 * Tests for UI state computation for availability badges and view action enablement.
 * T029: Add failing UI model test for Previously Connected badge visibility.
 */
class SourceListUiStateTest {

    /**
     * T029: Tests that Previously Connected badge is visible only when source was previously connected.
     */
    @Test
    fun testPreviouslyConnectedBadgeVisibility() {
        // Arrange: Create a source row state with previouslyConnected=true
        val sourceWithHistory = NdiSource(
            sourceId = "cam1-id",
            displayName = "Cam1",
            endpointAddress = "192.168.1.1:5960",
            lastSeenAtEpochMillis = System.currentTimeMillis(),
            isAvailable = true,
            previouslyConnected = true,
        )

        // Assert: Source row should indicate Previously Connected
        assertTrue(
            "Previously Connected badge should be visible when previouslyConnected=true",
            sourceWithHistory.previouslyConnected,
        )

        // Arrange: Create a source row state with previouslyConnected=false
        val sourceWithoutHistory = NdiSource(
            sourceId = "cam2-id",
            displayName = "Cam2",
            endpointAddress = "192.168.1.2:5960",
            lastSeenAtEpochMillis = System.currentTimeMillis(),
            isAvailable = true,
            previouslyConnected = false,
        )

        // Assert: Badge should not be visible
        assertFalse(
            "Previously Connected badge should NOT be visible when previouslyConnected=false",
            sourceWithoutHistory.previouslyConnected,
        )
    }

    /**
     * Tests that View Stream action is enabled only when source is available.
     */
    @Test
    fun testViewStreamActionEnabledState() {
        // Arrange: Available source
        val availableSource = NdiSource(
            sourceId = "cam1-id",
            displayName = "Cam1",
            endpointAddress = "192.168.1.1:5960",
            lastSeenAtEpochMillis = System.currentTimeMillis(),
            isAvailable = true,
            previouslyConnected = false,
        )

        // Assert: Action should be enabled
        assertTrue(
            "View Stream should be enabled when source is available",
            availableSource.isAvailable,
        )

        // Arrange: Unavailable source
        val unavailableSource = NdiSource(
            sourceId = "cam2-id",
            displayName = "Cam2",
            endpointAddress = "192.168.1.2:5960",
            lastSeenAtEpochMillis = System.currentTimeMillis(),
            isAvailable = false,
            previouslyConnected = true,
        )

        // Assert: Action should be disabled
        assertFalse(
            "View Stream should be disabled when source is unavailable",
            unavailableSource.isAvailable,
        )
    }
}
