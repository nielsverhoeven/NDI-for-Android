package com.ndi.app.navigation

import com.ndi.core.model.TelemetryEvent
import com.ndi.core.model.navigation.TopLevelDestination

fun interface NavigationTelemetryEmitter {
    fun emit(event: TelemetryEvent)
}

/**
 * Service-locator and event factory for top-level navigation telemetry.
 * Emits non-sensitive destination IDs and anonymized reason codes only.
 */
object TopLevelNavigationTelemetry {

    fun destinationSelected(
        from: TopLevelDestination,
        to: TopLevelDestination,
        trigger: String,
    ): TelemetryEvent = TelemetryEvent(
        name = TelemetryEvent.TOP_LEVEL_DESTINATION_SELECTED,
        timestampEpochMillis = System.currentTimeMillis(),
        attributes = mapOf(
            "from" to from.name,
            "to" to to.name,
            "trigger" to trigger,
        ),
    )

    fun destinationReselectedNoop(destination: TopLevelDestination): TelemetryEvent =
        TelemetryEvent(
            name = TelemetryEvent.TOP_LEVEL_DESTINATION_RESELECTED_NOOP,
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf("destination" to destination.name),
        )

    fun navigationFailed(to: TopLevelDestination, reasonCode: String): TelemetryEvent =
        TelemetryEvent(
            name = TelemetryEvent.TOP_LEVEL_NAVIGATION_FAILED,
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf(
                "to" to to.name,
                "reasonCode" to reasonCode,
            ),
        )

    fun homeDashboardViewed(): TelemetryEvent =
        TelemetryEvent(
            name = TelemetryEvent.HOME_DASHBOARD_VIEWED,
            timestampEpochMillis = System.currentTimeMillis(),
        )

    fun homeActionOpenStream(): TelemetryEvent =
        TelemetryEvent(
            name = TelemetryEvent.HOME_ACTION_OPEN_STREAM,
            timestampEpochMillis = System.currentTimeMillis(),
        )

    fun homeActionOpenView(): TelemetryEvent =
        TelemetryEvent(
            name = TelemetryEvent.HOME_ACTION_OPEN_VIEW,
            timestampEpochMillis = System.currentTimeMillis(),
        )
}

