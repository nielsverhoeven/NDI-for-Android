package com.ndi.app.navigation

import androidx.test.ext.junit.runners.AndroidJUnit4
import com.ndi.core.model.navigation.LaunchContext
import com.ndi.core.model.navigation.TopLevelDestination
import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.runner.RunWith

/**
 * Instrumentation tests for Recents/task-restore matrix reopening the last top-level destination
 * across Home, Stream, and View.
 *
 * FR-004a: Recents restore must reopen the last saved destination, not always Home.
 */
@RunWith(AndroidJUnit4::class)
class RecentsRestoreMatrixUiTest {

    private val coordinator = TopLevelNavigationCoordinator()

    @Test
    fun recentsRestore_withHome_reopensHome() {
        val result = coordinator.resolveInitialDestination(LaunchContext.RECENTS_RESTORE, TopLevelDestination.HOME)
        assertEquals(TopLevelDestination.HOME, result)
    }

    @Test
    fun recentsRestore_withStream_reopensStream() {
        val result = coordinator.resolveInitialDestination(LaunchContext.RECENTS_RESTORE, TopLevelDestination.STREAM)
        assertEquals(TopLevelDestination.STREAM, result)
    }

    @Test
    fun recentsRestore_withView_reopensView() {
        val result = coordinator.resolveInitialDestination(LaunchContext.RECENTS_RESTORE, TopLevelDestination.VIEW)
        assertEquals(TopLevelDestination.VIEW, result)
    }

    @Test
    fun recentsRestore_withNoSaved_defaultsToHome() {
        val result = coordinator.resolveInitialDestination(LaunchContext.RECENTS_RESTORE, null)
        assertEquals(TopLevelDestination.HOME, result)
    }

    @Test
    fun launcher_alwaysOpensHome_notLastSaved() {
        val resultWithStream = coordinator.resolveInitialDestination(LaunchContext.LAUNCHER, TopLevelDestination.STREAM)
        assertEquals(TopLevelDestination.HOME, resultWithStream)
        val resultWithView = coordinator.resolveInitialDestination(LaunchContext.LAUNCHER, TopLevelDestination.VIEW)
        assertEquals(TopLevelDestination.HOME, resultWithView)
    }
}

