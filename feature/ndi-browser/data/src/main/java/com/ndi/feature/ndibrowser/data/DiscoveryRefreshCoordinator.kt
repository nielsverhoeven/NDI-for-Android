package com.ndi.feature.ndibrowser.data

import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

class DiscoveryRefreshCoordinator(
    private val scope: CoroutineScope,
) {

    private var refreshJob: Job? = null

    fun start(intervalSeconds: Int, action: suspend () -> Unit) {
        if (refreshJob?.isActive == true) {
            return
        }

        refreshJob = scope.launch {
            action()
            while (true) {
                delay(intervalSeconds * 1000L)
                action()
            }
        }
    }

    fun stop() {
        refreshJob?.cancel()
        refreshJob = null
    }
}
