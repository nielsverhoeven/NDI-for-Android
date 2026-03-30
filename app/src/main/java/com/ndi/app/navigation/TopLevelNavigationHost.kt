package com.ndi.app.navigation

import androidx.navigation.NavController
import androidx.navigation.NavOptions
import com.ndi.app.R
import com.ndi.core.model.navigation.NavigationLayoutProfile
import com.ndi.core.model.navigation.NavigationTrigger
import com.ndi.core.model.navigation.TopLevelDestination

/**
 * Bridges the [TopLevelNavViewModel] events to the [NavController] and
 * Material navigation controls (BottomNavigationView / NavigationRailView).
 *
 * Enforces launchSingleTop + state-restore to prevent duplicate destination stacking.
 */
class TopLevelNavigationHost(
    private val navController: NavController,
    private val coordinator: TopLevelNavigationCoordinator = TopLevelNavigationCoordinator(),
    private val onSelectedItemChanged: (destinationId: Int) -> Unit = {},
) {

    /**
     * Navigate to the destination matching [destination] using single-top / state-restore rules.
     * If navigation fails, [onError] is invoked with a reason code.
     */
    fun navigateTo(
        destination: TopLevelDestination,
        trigger: NavigationTrigger,
        onError: (String) -> Unit = {},
    ) {
        val destinationId = destination.toNavId() ?: run {
            onError("invalid_destination_${destination.name}")
            return
        }

        val opts = coordinator.navOptions(
            graphStartDestinationId = R.id.homeDashboardFragment,
            trigger = trigger,
        )

        val navOptions = NavOptions.Builder()
            .setLaunchSingleTop(opts.launchSingleTop)
            .setRestoreState(opts.restoreState)
            .setPopUpTo(opts.popUpToId, inclusive = opts.popUpToInclusive, saveState = opts.saveState)
            .build()

        runCatching {
            navController.navigate(destinationId, null, navOptions)
            onSelectedItemChanged(destinationId)
        }.onFailure { error ->
            onError(error.message ?: "nav_controller_failure")
        }
    }

    /** Sync the navigation control selected item without triggering a navigation action. */
    fun syncSelectedItem(destination: TopLevelDestination) {
        destination.toNavId()?.let { onSelectedItemChanged(it) }
    }

    fun resolveDestination(navItemId: Int): TopLevelDestination? = when (navItemId) {
        R.id.homeDashboardFragment -> TopLevelDestination.HOME
        R.id.streamFragment -> TopLevelDestination.STREAM
        R.id.viewFragment -> TopLevelDestination.VIEW
        R.id.settingsFragment -> TopLevelDestination.SETTINGS
        else -> null
    }

    private fun TopLevelDestination.toNavId(): Int? = when (this) {
        TopLevelDestination.HOME -> R.id.homeDashboardFragment
        TopLevelDestination.STREAM -> R.id.streamFragment
        TopLevelDestination.VIEW -> R.id.viewFragment
        TopLevelDestination.SETTINGS -> R.id.settingsFragment
    }

    companion object {
        fun isTopLevelDestination(navItemId: Int): Boolean {
            val topLevelIds = setOf(
                R.id.homeDashboardFragment,
                R.id.streamFragment,
                R.id.viewFragment,
                R.id.settingsFragment,
            )
            return navItemId in topLevelIds
        }

        fun shouldShowTopLevelNav(profile: NavigationLayoutProfile): Boolean = true
    }
}


