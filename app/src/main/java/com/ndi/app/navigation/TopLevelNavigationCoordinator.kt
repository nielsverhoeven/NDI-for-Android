package com.ndi.app.navigation

import com.ndi.core.model.navigation.LaunchContext
import com.ndi.core.model.navigation.NavigationLayoutProfile
import com.ndi.core.model.navigation.NavigationTrigger
import com.ndi.core.model.navigation.TopLevelDestination
import com.ndi.core.model.navigation.TopLevelDestinationState

/**
 * Pure coordinator that enforces top-level destination selection rules:
 * - Re-selecting the current destination is a no-op (no duplicate stacking).
 * - Launcher context resolves to HOME.
 * - Recents restore may restore any valid last destination.
 */
class TopLevelNavigationCoordinator {

    companion object {
        private const val COMPACT_WIDTH_DP = 600
    }

    /**
     * Resolves the initial destination based on launch context and the persisted last destination.
     */
    fun resolveInitialDestination(
        launchContext: LaunchContext,
        lastSaved: TopLevelDestination?,
    ): TopLevelDestination {
        return when (launchContext) {
            LaunchContext.LAUNCHER -> TopLevelDestination.HOME
            LaunchContext.RECENTS_RESTORE -> lastSaved ?: TopLevelDestination.HOME
            LaunchContext.DEEP_LINK -> lastSaved ?: TopLevelDestination.HOME
            LaunchContext.IN_APP_SWITCH -> lastSaved ?: TopLevelDestination.HOME
        }
    }

    /**
     * Determines whether a destination switch is a no-op (already selected) or a real transition.
     */
    fun isNoOp(current: TopLevelDestination, requested: TopLevelDestination): Boolean =
        current == requested

    /**
     * Produces a new [TopLevelDestinationState] for a successful selection.
     */
    fun buildSelectedState(
        destination: TopLevelDestination,
        launchContext: LaunchContext,
    ): TopLevelDestinationState = TopLevelDestinationState(
        destination = destination,
        selectedAtEpochMillis = System.currentTimeMillis(),
        launchContext = launchContext,
    )

    /**
     * Resolves the adaptive layout profile from the current screen width.
     */
    fun resolveLayoutProfile(screenWidthDp: Int): NavigationLayoutProfile =
        if (screenWidthDp >= COMPACT_WIDTH_DP) {
            NavigationLayoutProfile.TABLET_NAV_RAIL
        } else {
            NavigationLayoutProfile.PHONE_BOTTOM_NAV
        }

    /**
     * Returns the NavOptions arguments to prevent duplicate destination stacking.
     * Callers should apply launchSingleTop + state-restore in the NavController.
     */
    fun navOptions(
        graphStartDestinationId: Int,
        trigger: NavigationTrigger,
    ): TopLevelNavOptions = TopLevelNavOptions(
        launchSingleTop = true,
        restoreState = trigger != NavigationTrigger.SYSTEM_RESTORE,
        saveState = true,
        popUpToId = graphStartDestinationId,
        popUpToInclusive = false,
    )

    /**
     * Deterministic back policy for the view flow:
     * - Viewer back returns to View root.
     * - View root back returns to Home.
     * Returns null when default system back should handle the transition.
     */
    fun resolveBackDestination(
        currentTopLevelDestination: TopLevelDestination,
        isViewerVisible: Boolean,
    ): TopLevelDestination? = when {
        isViewerVisible && currentTopLevelDestination == TopLevelDestination.VIEW -> TopLevelDestination.VIEW
        !isViewerVisible && currentTopLevelDestination == TopLevelDestination.VIEW -> TopLevelDestination.HOME
        else -> null
    }
}

data class TopLevelNavOptions(
    val launchSingleTop: Boolean,
    val restoreState: Boolean,
    val saveState: Boolean,
    val popUpToId: Int,
    val popUpToInclusive: Boolean,
)

