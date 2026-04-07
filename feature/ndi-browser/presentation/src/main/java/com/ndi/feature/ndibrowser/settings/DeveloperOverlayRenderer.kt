package com.ndi.feature.ndibrowser.settings

import android.view.View
import android.widget.TextView
import androidx.core.view.isVisible
import com.ndi.core.model.NdiOverlayMode

object DeveloperOverlayRenderer {

    fun render(
        container: View,
        streamStatusView: TextView,
        sessionIdView: TextView,
        recentLogsView: TextView,
        overlayDisplayState: OverlayDisplayState?,
    ) {
        val state = overlayDisplayState
        val visible = state != null && state.mode != NdiOverlayMode.DISABLED
        container.isVisible = visible
        if (!visible) {
            streamStatusView.text = ""
            sessionIdView.text = ""
            sessionIdView.isVisible = false
            recentLogsView.text = ""
            recentLogsView.isVisible = false
            return
        }

        val statusLabel = when (state.mode) {
            NdiOverlayMode.ACTIVE -> state.streamStatus ?: "ACTIVE"
            NdiOverlayMode.IDLE -> "IDLE"
            NdiOverlayMode.DISABLED -> ""
        }
        streamStatusView.text = "Status: $statusLabel"

        sessionIdView.text = state.sessionId?.let { "Session: $it" }.orEmpty()
        sessionIdView.isVisible = !state.sessionId.isNullOrBlank()

        recentLogsView.text = state.recentLogs.joinToString(separator = "\n")
        recentLogsView.isVisible = state.recentLogs.isNotEmpty()
    }

    fun renderDiscoveryDiagnostics(
        discoveryDiagnosticsView: TextView,
        overlayDisplayState: OverlayDisplayState?,
    ) {
        val diagnostics = overlayDisplayState?.discoveryDiagnostics
        if (diagnostics == null || overlayDisplayState.mode == NdiOverlayMode.DISABLED) {
            discoveryDiagnosticsView.isVisible = false
            discoveryDiagnosticsView.text = ""
            return
        }
        val summary = buildString {
            append("Discovery: ")
            if (diagnostics.serverStatusRollup.isEmpty()) {
                append("no servers checked")
            } else {
                diagnostics.serverStatusRollup.forEach { status ->
                    append("[${status.serverId.take(8)} ${status.outcome.name}] ")
                }
            }

            if (overlayDisplayState.compatibilityMessages.isNotEmpty()) {
                append("\nCompatibility guidance:")
                overlayDisplayState.compatibilityMessages.forEach { line ->
                    append("\n- ")
                    append(line)
                }
            }
        }
        discoveryDiagnosticsView.text = summary
        discoveryDiagnosticsView.isVisible = true
    }
}