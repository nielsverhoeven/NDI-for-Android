package com.ndi.feature.ndibrowser.data

import kotlinx.coroutines.delay

data class ViewerReconnectResult(
    val recovered: Boolean,
    val attempts: Int,
)

class ViewerReconnectCoordinator(
    private val retryDelayMillis: Long = 1000L,
) {

    suspend fun retryWithinWindow(windowSeconds: Int, attempt: suspend () -> Boolean): ViewerReconnectResult {
        val attemptsAllowed = windowSeconds.coerceAtLeast(1)
        repeat(attemptsAllowed) { index ->
            if (attempt()) {
                return ViewerReconnectResult(recovered = true, attempts = index + 1)
            }
            if (index < attemptsAllowed - 1) {
                delay(retryDelayMillis)
            }
        }
        return ViewerReconnectResult(recovered = false, attempts = attemptsAllowed)
    }
}
