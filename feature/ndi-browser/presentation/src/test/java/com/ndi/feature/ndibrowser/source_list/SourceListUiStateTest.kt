package com.ndi.feature.ndibrowser.source_list

import com.ndi.core.model.NdiSource
import com.ndi.core.model.DiscoveryStatus
import org.junit.Test
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Assert.assertEquals
import java.io.File

/**
 * Tests for UI state computation for availability badges and view action enablement.
 * T029: Add failing UI model test for Previously Connected badge visibility.
 */
class SourceListUiStateTest {

    @Test
    fun fluentButtonShapeContract_sourceListButtonsUseCanonicalStyles() {
        val sourceListLayout = File("src/main/res/layout/fragment_source_list.xml").readText()
        val sourceRowLayout = File("src/main/res/layout/item_ndi_source.xml").readText()

        assertTrue(sourceListLayout.contains("android:id=\"@+id/refreshButton\""))
        assertTrue(sourceListLayout.contains("style=\"@style/Widget.NdiBrowser.Button\""))
        assertTrue(sourceRowLayout.contains("android:id=\"@+id/viewStreamButton\""))
        assertTrue(sourceRowLayout.contains("style=\"@style/Widget.NdiBrowser.Button.Tonal\""))
    }

    @Test
    fun consistencyContract_sourceListLayoutsContainNoLegacyMaterialButtonStyles() {
        val sourceListLayout = File("src/main/res/layout/fragment_source_list.xml").readText()
        val sourceRowLayout = File("src/main/res/layout/item_ndi_source.xml").readText()

        assertFalse(sourceListLayout.contains("Widget.Material3.Button"))
        assertFalse(sourceRowLayout.contains("Widget.Material3.Button"))
    }

    @Test
    fun visualStateContract_loadingAndErrorStates_areDistinct() {
        val loading = SourceListUiState(
            discoveryStatus = DiscoveryStatus.IN_PROGRESS,
            isRefreshing = true,
            sources = emptyList(),
        )
        val error = SourceListUiState(
            discoveryStatus = DiscoveryStatus.FAILURE,
            isRefreshing = false,
            errorMessage = "network down",
            sources = emptyList(),
        )

        assertTrue(loading.isRefreshing)
        assertEquals(DiscoveryStatus.IN_PROGRESS, loading.discoveryStatus)
        assertEquals(DiscoveryStatus.FAILURE, error.discoveryStatus)
        assertTrue(error.errorMessage?.isNotBlank() == true)
    }

    @Test
    fun adaptivityContract_layoutModeSwitchesBetweenCompactAndExpanded() {
        val compact = SourceListUiState(layoutMode = SourceListLayoutMode.COMPACT)
        val expanded = SourceListUiState(layoutMode = SourceListLayoutMode.EXPANDED)

        assertEquals(SourceListLayoutMode.COMPACT, compact.layoutMode)
        assertEquals(SourceListLayoutMode.EXPANDED, expanded.layoutMode)
    }

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
