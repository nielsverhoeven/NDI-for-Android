package com.ndi.feature.ndibrowser.data.mapper

import com.ndi.core.model.OutputSession
import com.ndi.core.model.OutputConsentState
import com.ndi.core.model.OutputInputKind
import com.ndi.core.model.OutputState
import java.util.UUID

class OutputSessionMapper {

    fun createStartingSession(
        inputSourceId: String,
        preferredName: String,
        activeStreamNames: Set<String>,
        inputSourceKind: OutputInputKind = OutputInputKind.DISCOVERED_NDI,
        consentState: OutputConsentState = OutputConsentState.NOT_REQUIRED,
        hostInstanceId: String = "local",
    ): OutputSession {
        val normalizedName = normalizeName(preferredName, inputSourceId)
        val uniqueName = resolveUniqueName(normalizedName, activeStreamNames)
        return OutputSession(
            sessionId = UUID.randomUUID().toString(),
            inputSourceId = inputSourceId,
            inputSourceKind = inputSourceKind,
            outboundStreamName = uniqueName,
            consentState = consentState,
            state = OutputState.STARTING,
            startedAtEpochMillis = System.currentTimeMillis(),
            hostInstanceId = hostInstanceId,
        )
    }

    fun resolveUniqueName(baseName: String, activeStreamNames: Set<String>): String {
        val normalizedActiveNames = activeStreamNames.map { it.lowercase() }.toSet()
        if (baseName.lowercase() !in normalizedActiveNames) return baseName
        var suffix = 2
        while ("$baseName ($suffix)".lowercase() in normalizedActiveNames) {
            suffix++
        }
        return "$baseName ($suffix)"
    }

    private fun normalizeName(preferredName: String, inputSourceId: String): String {
        val candidate = preferredName.trim().ifBlank { "NDI Output" }
        return candidate.filter { it.code in 32..126 }.ifBlank { inputSourceId }
    }
}
