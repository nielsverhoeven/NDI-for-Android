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

    fun viewSelectionOpenedViewer(sourceId: String): TelemetryEvent =
        TelemetryEvent(
            name = TelemetryEvent.VIEW_SELECTION_OPENED_VIEWER,
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf("sourceId" to sourceId),
        )

    fun viewBackToRoot(): TelemetryEvent =
        TelemetryEvent(
            name = TelemetryEvent.VIEW_BACK_TO_ROOT,
            timestampEpochMillis = System.currentTimeMillis(),
        )

    fun viewRootBackToHome(): TelemetryEvent =
        TelemetryEvent(
            name = TelemetryEvent.VIEW_ROOT_BACK_TO_HOME,
            timestampEpochMillis = System.currentTimeMillis(),
        )

    fun versionSupportWindowEvaluated(
        lowestSupportedMajor: Int,
        highestSupportedMajor: Int,
    ): TelemetryEvent =
        TelemetryEvent(
            name = TelemetryEvent.VERSION_SUPPORT_WINDOW_EVALUATED,
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf(
                "lowestSupportedMajor" to lowestSupportedMajor.toString(),
                "highestSupportedMajor" to highestSupportedMajor.toString(),
            ),
        )

    fun unsupportedVersionFailFast(
        role: String,
        sdkInt: Int,
        majorVersion: Int,
    ): TelemetryEvent =
        TelemetryEvent(
            name = TelemetryEvent.UNSUPPORTED_VERSION_FAIL_FAST,
            timestampEpochMillis = System.currentTimeMillis(),
            attributes = mapOf(
                "role" to role,
                "sdkInt" to sdkInt.toString(),
                "majorVersion" to majorVersion.toString(),
            ),
        )
}

