package com.ndi.feature.ndibrowser.data.mapper

import com.ndi.core.model.NdiSource

class NdiSourceMapper {

    fun map(rawSources: List<NdiSource>): List<NdiSource> {
        return rawSources
            .filter { it.sourceId.isNotBlank() }
            .distinctBy { it.sourceId }
            .sortedWith(compareBy(NdiSource::displayName, NdiSource::sourceId))
    }
}
