package com.ndi.feature.ndibrowser.data.logging

import java.time.Instant

data class DeveloperLogEntry(
    val timestamp: Instant,
    val eventType: String,
    val message: String,
    val configuredAddresses: List<String>,
    val isDeveloperModeEnabled: Boolean,
) {
    companion object {
        fun create(
            timestamp: Instant,
            eventType: String,
            message: String,
            configuredAddresses: List<String>,
            isDeveloperModeEnabled: Boolean,
        ): DeveloperLogEntry {
            return DeveloperLogEntry(
                timestamp = timestamp,
                eventType = eventType,
                message = message,
                configuredAddresses = if (isDeveloperModeEnabled) configuredAddresses else emptyList(),
                isDeveloperModeEnabled = isDeveloperModeEnabled,
            )
        }
    }
}
