package com.ndi.core.model.navigation

import com.ndi.core.model.OutputState
import com.ndi.core.model.PlaybackState

/** The four reachable top-level destinations. */
enum class TopLevelDestination { HOME, STREAM, VIEW, SETTINGS }

/** How the app was opened or navigation initiated. */
enum class LaunchContext {
    LAUNCHER,
    RECENTS_RESTORE,
    DEEP_LINK,
    IN_APP_SWITCH,
}

/** Which navigation control surface triggered the switch. */
enum class NavigationTrigger {
    BOTTOM_NAV,
    NAV_RAIL,
    HOME_ACTION,
    DEEP_LINK,
    SYSTEM_RESTORE,
}

/** Outcome of a top-level navigation attempt. */
enum class NavigationOutcome {
    SUCCESS,
    NO_OP_ALREADY_SELECTED,
    FAILED_INVALID_ROUTE,
    FAILED_NAV_CONTROLLER,
}

/** Which adaptive layout profile is active. */
enum class NavigationLayoutProfile {
    PHONE_BOTTOM_NAV,
    TABLET_NAV_RAIL,
}

/**
 * Represents the currently selected top-level destination and selection metadata.
 */
data class TopLevelDestinationState(
    val destination: TopLevelDestination,
    val selectedAtEpochMillis: Long,
    val launchContext: LaunchContext,
    val restoredFromProcessDeath: Boolean = false,
)

/**
 * Snapshot of aggregate status shown on the Home dashboard.
 * Must contain only non-sensitive metadata.
 */
data class HomeDashboardSnapshot(
    val generatedAtEpochMillis: Long,
    val streamStatus: OutputState,
    val streamSourceId: String? = null,
    val selectedViewSourceId: String? = null,
    val selectedViewSourceDisplayName: String? = null,
    val viewPlaybackStatus: PlaybackState,
    val canNavigateToStream: Boolean = true,
    val canNavigateToView: Boolean = true,
)

/**
 * Records a single top-level navigation attempt for deterministic routing and telemetry.
 */
data class NavigationTransitionRecord(
    val transitionId: String,
    val fromDestination: TopLevelDestination,
    val toDestination: TopLevelDestination,
    val trigger: NavigationTrigger,
    val outcome: NavigationOutcome,
    val occurredAtEpochMillis: Long,
    val failureReasonCode: String? = null,
)

/**
 * Models continuity behavior for the Stream destination when navigating away or restoring after
 * process death. Active output keeps running; auto-restart is prohibited.
 */
enum class BackgroundContinuationReason {
    NONE,
    APP_BACKGROUND,
}

data class StreamContinuityState(
    val hasActiveOutput: Boolean,
    val outputState: OutputState,
    val lastKnownOutputSourceId: String? = null,
    val lastKnownStreamName: String? = null,
    val runningWhileBackgrounded: Boolean = false,
    val backgroundReason: BackgroundContinuationReason = BackgroundContinuationReason.NONE,
    val lastBackgroundedAtEpochMillis: Long? = null,
    val restoredAfterProcessDeath: Boolean = false,
    /** Always false in this feature version. */
    val autoRestartPermitted: Boolean = false,
)

/**
 * Models selection/viewing continuity for the View destination during navigation and
 * process restoration. Leaving View stops playback. Autoplay is prohibited.
 */
data class ViewContinuityState(
    val selectedSourceId: String? = null,
    val selectedSourceDisplayName: String? = null,
    val playbackState: PlaybackState,
    val stoppedByTopLevelNavigation: Boolean = false,
    val restoredAfterProcessDeath: Boolean = false,
    /** Always false in this feature version. */
    val autoplayPermitted: Boolean = false,
)

/** Deterministic back policy for the View -> Viewer flow. */
enum class ViewBackPolicy {
    VIEWER_BACK_TO_VIEW_ROOT,
    VIEW_ROOT_BACK_TO_HOME,
}

/** Canonical transition labels used for view-flow telemetry and assertions. */
enum class ViewNavigationTransition {
    VIEW_ROOT_TO_VIEWER,
    VIEWER_TO_VIEW_ROOT,
    VIEW_ROOT_TO_HOME,
}

/**
 * Session-level navigation state for the View flow.
 * Keeps deterministic transition and back-policy metadata in one canonical structure.
 */
data class ViewNavigationSessionState(
    val enteredFromDestination: TopLevelDestination,
    val selectedSourceId: String? = null,
    val viewerOpen: Boolean = false,
    val lastTransition: ViewNavigationTransition? = null,
    val backPolicy: Set<ViewBackPolicy> = setOf(
        ViewBackPolicy.VIEWER_BACK_TO_VIEW_ROOT,
        ViewBackPolicy.VIEW_ROOT_BACK_TO_HOME,
    ),
    val updatedAtEpochMillis: Long = System.currentTimeMillis(),
)

