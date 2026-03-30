package com.ndi.feature.ndibrowser.data.util

import java.util.UUID

object CorrelationId {
    fun generate(): String = UUID.randomUUID().toString()
}
