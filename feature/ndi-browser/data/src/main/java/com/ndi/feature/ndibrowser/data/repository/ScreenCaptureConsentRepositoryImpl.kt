package com.ndi.feature.ndibrowser.data.repository

import com.ndi.feature.ndibrowser.domain.repository.ScreenCaptureConsentRepository
import com.ndi.feature.ndibrowser.domain.repository.ScreenCaptureConsentState
import java.util.concurrent.ConcurrentHashMap

class ScreenCaptureConsentRepositoryImpl : ScreenCaptureConsentRepository {

    private val states = ConcurrentHashMap<String, ScreenCaptureConsentState>()

    override suspend fun beginConsentRequest(inputSourceId: String) {
        states[inputSourceId] = ScreenCaptureConsentState(
            sourceId = inputSourceId,
            granted = false,
            tokenRef = null,
        )
    }

    override suspend fun registerConsentResult(
        inputSourceId: String,
        granted: Boolean,
        tokenRef: String?,
    ): ScreenCaptureConsentState {
        val state = ScreenCaptureConsentState(
            sourceId = inputSourceId,
            granted = granted,
            tokenRef = if (granted) tokenRef else null,
        )
        states[inputSourceId] = state
        return state
    }

    override suspend fun getConsentState(inputSourceId: String): ScreenCaptureConsentState? {
        return states[inputSourceId]
    }

    override suspend fun clearConsent(inputSourceId: String) {
        states.remove(inputSourceId)
    }
}

