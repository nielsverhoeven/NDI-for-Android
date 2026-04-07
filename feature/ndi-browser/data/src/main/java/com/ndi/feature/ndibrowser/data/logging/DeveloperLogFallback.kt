package com.ndi.feature.ndibrowser.data.logging

object DeveloperLogFallback {
    const val FALLBACK_NO_VALID_ADDRESSES = "not configured"

    fun getFallbackMessage(reason: String): String {
        return if (reason.isBlank()) {
            FALLBACK_NO_VALID_ADDRESSES
        } else {
            FALLBACK_NO_VALID_ADDRESSES
        }
    }
}
