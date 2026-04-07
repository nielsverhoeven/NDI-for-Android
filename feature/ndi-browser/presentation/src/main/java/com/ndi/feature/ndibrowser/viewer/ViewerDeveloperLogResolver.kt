package com.ndi.feature.ndibrowser.viewer

import com.ndi.feature.ndibrowser.domain.repository.AddressValidation
import com.ndi.feature.ndibrowser.settings.OverlayDisplayState
import com.ndi.core.model.NdiOverlayMode

class ViewerDeveloperLogResolver(
    private val addressValidation: AddressValidation,
) {

    fun resolve(overlayDisplayState: OverlayDisplayState?): OverlayDisplayState? {
        val state = overlayDisplayState ?: return null
        if (state.mode == NdiOverlayMode.DISABLED) return state
        if (state.recentLogs.isEmpty()) return state

        val validAddresses = addressValidation.validateAndFilterAddresses(state.configuredAddresses)
        val replacement = if (validAddresses.isEmpty()) {
            FALLBACK_NO_VALID_ADDRESSES
        } else {
            addressValidation.getDisplayText(validAddresses)
        }

        val resolvedLogs = state.recentLogs.map { line ->
            line.replace(REDACTED_IP_TOKEN, replacement)
        }

        return state.copy(recentLogs = resolvedLogs)
    }

    companion object {
        private const val REDACTED_IP_TOKEN = "[redacted-ip]"
        private const val FALLBACK_NO_VALID_ADDRESSES = "not configured"
    }
}
