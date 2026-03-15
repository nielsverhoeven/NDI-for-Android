package com.ndi.core.model

data class TelemetryEvent(
    val name: String,
    val timestampEpochMillis: Long,
    val attributes: Map<String, String> = emptyMap(),
)
