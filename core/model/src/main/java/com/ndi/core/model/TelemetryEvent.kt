package com.ndi.core.model

data class TelemetryEvent(
    val name: String,
    val timestampEpochMillis: Long,
    val attributes: Map<String, String> = emptyMap(),
) {
    companion object {
        const val OUTPUT_START_REQUESTED = "output_start_requested"
        const val OUTPUT_START_IGNORED_DUPLICATE = "output_start_ignored_duplicate"
        const val OUTPUT_STARTED = "output_started"
        const val OUTPUT_SCREEN_SHARE_CONSENT_REQUESTED = "output_screen_share_consent_requested"
        const val OUTPUT_SCREEN_SHARE_CONSENT_GRANTED = "output_screen_share_consent_granted"
        const val OUTPUT_SCREEN_SHARE_CONSENT_DENIED = "output_screen_share_consent_denied"
        const val OUTPUT_STOP_IGNORED_DUPLICATE = "output_stop_ignored_duplicate"
        const val OUTPUT_STOPPED = "output_stopped"
        const val OUTPUT_INTERRUPTED = "output_interrupted"
        const val OUTPUT_RETRY_REQUESTED = "output_retry_requested"
        const val OUTPUT_RETRY_SUCCEEDED = "output_retry_succeeded"
        const val OUTPUT_RETRY_FAILED = "output_retry_failed"
        const val DUAL_EMULATOR_E2E_PREFLIGHT_PASSED = "dual_emulator_e2e_preflight_passed"
        const val DUAL_EMULATOR_E2E_STARTED = "dual_emulator_e2e_started"
        const val DUAL_EMULATOR_E2E_PASSED = "dual_emulator_e2e_passed"
        const val DUAL_EMULATOR_E2E_FAILED = "dual_emulator_e2e_failed"

        // Top-level navigation telemetry (spec 003)
        const val TOP_LEVEL_DESTINATION_SELECTED = "top_level_destination_selected"
        const val TOP_LEVEL_DESTINATION_RESELECTED_NOOP = "top_level_destination_reselected_noop"
        const val TOP_LEVEL_NAVIGATION_FAILED = "top_level_navigation_failed"
        const val HOME_DASHBOARD_VIEWED = "home_dashboard_viewed"
        const val HOME_ACTION_OPEN_STREAM = "home_action_open_stream"
        const val HOME_ACTION_OPEN_VIEW = "home_action_open_view"
    }
}
