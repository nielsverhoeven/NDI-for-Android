package com.ndi.app.navigation

import android.os.Bundle
import androidx.core.net.toUri
import androidx.navigation.NavDeepLinkRequest
import com.ndi.app.R
import com.ndi.core.model.navigation.TopLevelDestination

object NdiNavigation {

    enum class SettingsToggleAction {
        OPEN,
        CLOSE,
        NONE,
    }

    fun viewerRequest(sourceId: String): NavDeepLinkRequest {
        return NavDeepLinkRequest.Builder
            .fromUri("ndi://viewer/$sourceId".toUri())
            .build()
    }

    fun outputRequest(sourceId: String): NavDeepLinkRequest {
        return NavDeepLinkRequest.Builder
            .fromUri("ndi://output/$sourceId".toUri())
            .build()
    }

    fun sourceIdFrom(args: Bundle?): String {
        return args?.getString("sourceId").orEmpty()
    }

    fun outputSourceIdFrom(args: Bundle?): String {
        return args?.getString("sourceId").orEmpty()
    }

    // ---- Top-level destination route IDs (spec 003) ----

    fun homeDestinationId(): Int = R.id.homeDashboardFragment

    fun streamDestinationId(): Int = R.id.streamFragment

    fun viewDestinationId(): Int = R.id.viewFragment

    fun viewerDestinationId(): Int = R.id.viewerHostFragment

    fun viewToViewerActionId(): Int = R.id.action_viewFragment_to_viewerHostFragment

    fun viewerToViewRootActionId(): Int = R.id.action_viewerHostFragment_to_viewFragment

    fun shouldViewerBackNavigateToViewRoot(currentDestinationId: Int): Boolean =
        currentDestinationId == R.id.viewerHostFragment

    fun shouldViewRootBackNavigateHome(currentDestinationId: Int): Boolean =
        currentDestinationId == R.id.viewFragment

    fun topLevelDestinationId(destination: TopLevelDestination): Int = when (destination) {
        TopLevelDestination.HOME -> R.id.homeDashboardFragment
        TopLevelDestination.STREAM -> R.id.streamFragment
        TopLevelDestination.VIEW -> R.id.viewFragment
        TopLevelDestination.SETTINGS -> R.id.settingsFragment
    }

    // ---- Spec 006: Settings navigation helpers ----

    fun settingsRequest(): NavDeepLinkRequest {
        return NavDeepLinkRequest.Builder
            .fromUri("ndi://settings".toUri())
            .build()
    }

    fun settingsDestinationId(): Int = R.id.settingsFragment

    fun sourceListToSettingsActionId(): Int = R.id.action_streamFragment_to_settingsFragment

    fun viewerToSettingsActionId(): Int = R.id.action_viewerHostFragment_to_settingsFragment

    fun outputToSettingsActionId(): Int = R.id.action_outputControlFragment_to_settingsFragment

    fun resolveSettingsToggleAction(currentDestinationId: Int): SettingsToggleAction {
        return when {
            currentDestinationId == settingsDestinationId() -> SettingsToggleAction.CLOSE
            else -> SettingsToggleAction.OPEN
        }
    }

    fun shouldOpenSettings(currentDestinationId: Int): Boolean {
        return resolveSettingsToggleAction(currentDestinationId) == SettingsToggleAction.OPEN
    }

    fun shouldCloseSettings(currentDestinationId: Int): Boolean {
        return resolveSettingsToggleAction(currentDestinationId) == SettingsToggleAction.CLOSE
    }
}
