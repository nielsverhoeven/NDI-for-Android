package com.ndi.feature.ndibrowser.data.model

sealed interface DisconnectionEvent {
    data class Detected(
        val sourceId: String,
        val reason: String,
        val timestampEpochMillis: Long = System.currentTimeMillis(),
    ) : DisconnectionEvent

    data class RecoveryStarted(
        val sourceId: String,
        val attempt: Int,
        val maxAttempts: Int,
        val timestampEpochMillis: Long = System.currentTimeMillis(),
    ) : DisconnectionEvent

    data class Recovered(
        val sourceId: String,
        val attempts: Int,
        val timestampEpochMillis: Long = System.currentTimeMillis(),
    ) : DisconnectionEvent

    data class Failed(
        val sourceId: String,
        val attempts: Int,
        val reason: String,
        val timestampEpochMillis: Long = System.currentTimeMillis(),
    ) : DisconnectionEvent
}
