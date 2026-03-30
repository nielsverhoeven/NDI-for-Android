package com.ndi.feature.ndibrowser.data

import kotlinx.coroutines.delay

data class OutputRecoveryResult(
    val recovered: Boolean,
    val attempts: Int,
)

class OutputRecoveryCoordinator(
    private val retryDelayMillis: Long = 1000L,
) {

    suspend fun retryWithinWindow(
        windowSeconds: Int,
        attempt: suspend (attemptNo: Int) -> Boolean,
    ): OutputRecoveryResult {
        val attemptsAllowed = windowSeconds.coerceAtLeast(1)
        repeat(attemptsAllowed) { index ->
            if (attempt(index + 1)) {
                return OutputRecoveryResult(recovered = true, attempts = index + 1)
            }
            if (index < attemptsAllowed - 1) {
                delay(retryDelayMillis)
            }
        }
        return OutputRecoveryResult(recovered = false, attempts = attemptsAllowed)
    }
}