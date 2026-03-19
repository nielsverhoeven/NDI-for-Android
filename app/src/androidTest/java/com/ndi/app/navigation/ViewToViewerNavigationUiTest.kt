package com.ndi.app.navigation

import androidx.core.net.toUri
import androidx.test.ext.junit.runners.AndroidJUnit4
import com.ndi.core.model.navigation.TopLevelDestination
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotEquals
import org.junit.Test
import org.junit.runner.RunWith

/**
 * Device-side contract assertions for View -> Viewer routing and deterministic back behavior.
 */
@RunWith(AndroidJUnit4::class)
class ViewToViewerNavigationUiTest {

    @Test
    fun selectingSourceFromView_routesToViewer() {
        val sourceId = "camera-1"
        val viewerUri = NdiNavigation.viewerRequest(sourceId).uri.toString()
        val outputUri = NdiNavigation.outputRequest(sourceId).uri.toString()

        assertEquals("ndi://viewer/$sourceId", viewerUri)
        assertEquals("ndi://viewer/$sourceId".toUri(), NdiNavigation.viewerRequest(sourceId).uri)
        assertNotEquals(outputUri, viewerUri)
    }

    @Test
    fun backFromViewer_returnsToViewRoot() {
        val coordinator = TopLevelNavigationCoordinator()

        val destination = coordinator.resolveBackDestination(
            currentTopLevelDestination = TopLevelDestination.VIEW,
            isViewerVisible = true,
        )

        assertEquals(TopLevelDestination.VIEW, destination)
    }

    @Test
    fun backFromViewRoot_returnsHome() {
        val coordinator = TopLevelNavigationCoordinator()

        val destination = coordinator.resolveBackDestination(
            currentTopLevelDestination = TopLevelDestination.VIEW,
            isViewerVisible = false,
        )

        assertEquals(TopLevelDestination.HOME, destination)
    }
}
