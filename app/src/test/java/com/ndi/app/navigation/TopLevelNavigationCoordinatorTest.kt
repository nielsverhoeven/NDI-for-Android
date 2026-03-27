package com.ndi.app.navigation

import com.ndi.app.R
import com.ndi.core.model.navigation.LaunchContext
import com.ndi.core.model.navigation.NavigationLayoutProfile
import com.ndi.core.model.navigation.NavigationTrigger
import com.ndi.core.model.navigation.TopLevelDestination
import org.junit.Assert.assertNull
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class TopLevelNavigationCoordinatorTest {

    private val coordinator = TopLevelNavigationCoordinator()

    @Test
    fun launcherContext_alwaysResolvesToHome() {
        assertEquals(
            TopLevelDestination.HOME,
            coordinator.resolveInitialDestination(LaunchContext.LAUNCHER, TopLevelDestination.STREAM),
        )
    }

    @Test
    fun recentsRestore_withSavedStream_restoresStream() {
        assertEquals(
            TopLevelDestination.STREAM,
            coordinator.resolveInitialDestination(LaunchContext.RECENTS_RESTORE, TopLevelDestination.STREAM),
        )
    }

    @Test
    fun recentsRestore_withNoSaved_defaultsToHome() {
        assertEquals(
            TopLevelDestination.HOME,
            coordinator.resolveInitialDestination(LaunchContext.RECENTS_RESTORE, null),
        )
    }

    @Test
    fun isNoOp_returnsTrueForSameDestination() {
        assertTrue(coordinator.isNoOp(TopLevelDestination.HOME, TopLevelDestination.HOME))
        assertTrue(coordinator.isNoOp(TopLevelDestination.STREAM, TopLevelDestination.STREAM))
        assertTrue(coordinator.isNoOp(TopLevelDestination.VIEW, TopLevelDestination.VIEW))
    }

    @Test
    fun isNoOp_returnsFalseForDifferentDestination() {
        assertFalse(coordinator.isNoOp(TopLevelDestination.HOME, TopLevelDestination.STREAM))
        assertFalse(coordinator.isNoOp(TopLevelDestination.STREAM, TopLevelDestination.VIEW))
    }

    @Test
    fun resolveLayoutProfile_compactWidth_returnsBottomNav() {
        assertEquals(
            NavigationLayoutProfile.PHONE_BOTTOM_NAV,
            coordinator.resolveLayoutProfile(360),
        )
        assertEquals(
            NavigationLayoutProfile.PHONE_BOTTOM_NAV,
            coordinator.resolveLayoutProfile(599),
        )
    }

    @Test
    fun resolveLayoutProfile_expandedWidth_returnsNavRail() {
        assertEquals(
            NavigationLayoutProfile.TABLET_NAV_RAIL,
            coordinator.resolveLayoutProfile(600),
        )
        assertEquals(
            NavigationLayoutProfile.TABLET_NAV_RAIL,
            coordinator.resolveLayoutProfile(900),
        )
    }

    @Test
    fun navOptions_launchSingleTopIsAlwaysTrue() {
        val opts = coordinator.navOptions(1, NavigationTrigger.BOTTOM_NAV)
        assertTrue(opts.launchSingleTop)
    }

    @Test
    fun navOptions_restoreState_falseForSystemRestore() {
        val opts = coordinator.navOptions(1, NavigationTrigger.SYSTEM_RESTORE)
        assertFalse(opts.restoreState)
    }

    @Test
    fun navOptions_restoreState_trueForBottomNav() {
        val opts = coordinator.navOptions(1, NavigationTrigger.BOTTOM_NAV)
        assertTrue(opts.restoreState)
    }

    @Test
    fun viewRouteActionIds_pointToViewerAndBackToViewRoot() {
        assertEquals(
            com.ndi.app.R.id.action_viewFragment_to_viewerHostFragment,
            NdiNavigation.viewToViewerActionId(),
        )
        assertEquals(
            com.ndi.app.R.id.action_viewerHostFragment_to_viewFragment,
            NdiNavigation.viewerToViewRootActionId(),
        )
    }

    @Test
    fun resolveBackDestination_viewerBack_returnsViewRoot() {
        val resolved = coordinator.resolveBackDestination(
            currentTopLevelDestination = TopLevelDestination.VIEW,
            isViewerVisible = true,
        )
        assertEquals(TopLevelDestination.VIEW, resolved)
    }

    @Test
    fun resolveBackDestination_viewRootBack_returnsHome() {
        val resolved = coordinator.resolveBackDestination(
            currentTopLevelDestination = TopLevelDestination.VIEW,
            isViewerVisible = false,
        )
        assertEquals(TopLevelDestination.HOME, resolved)
    }

    @Test
    fun resolveBackDestination_nonView_returnsNull() {
        val streamResolved = coordinator.resolveBackDestination(
            currentTopLevelDestination = TopLevelDestination.STREAM,
            isViewerVisible = false,
        )
        assertNull(streamResolved)
    }

    @Test
    fun settingsRoute_resolvesToSettingsFragmentId() {
        assertEquals(R.id.settingsFragment, NdiNavigation.topLevelDestinationId(TopLevelDestination.SETTINGS))
    }

    @Test
    fun settings_isRecognizedAsTopLevelDestination() {
        assertTrue(TopLevelNavigationHost.isTopLevelDestination(R.id.settingsFragment))
    }

    @Test
    fun navOptions_forBottomNav_enablesSingleTopAndRestoreState() {
        val opts = coordinator.navOptions(R.id.settingsFragment, NavigationTrigger.BOTTOM_NAV)
        assertTrue(opts.launchSingleTop)
        assertTrue(opts.restoreState)
    }

    @Test
    fun isNoOp_returnsTrueForSettingsReselect() {
        assertTrue(coordinator.isNoOp(TopLevelDestination.SETTINGS, TopLevelDestination.SETTINGS))
    }
}

