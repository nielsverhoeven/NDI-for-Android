package com.ndi.feature.ndibrowser.data.mapper

import com.ndi.core.database.ConnectionHistoryStateEntity
import com.ndi.core.database.LastViewedContextEntity
import com.ndi.feature.ndibrowser.domain.repository.ConnectionHistoryState
import com.ndi.feature.ndibrowser.domain.repository.LastViewedContext

class ViewerContinuityMapper {

    fun toLastViewedContext(entity: LastViewedContextEntity): LastViewedContext {
        return LastViewedContext(
            contextId = entity.contextId,
            sourceId = entity.sourceId,
            lastFrameImagePath = entity.lastFrameImagePath,
            lastFrameCapturedAtEpochMillis = entity.lastFrameCapturedAtEpochMillis,
            restoredAtEpochMillis = entity.restoredAtEpochMillis,
        )
    }

    fun toLastViewedContextEntity(model: LastViewedContext): LastViewedContextEntity {
        return LastViewedContextEntity(
            contextId = model.contextId,
            sourceId = model.sourceId,
            lastFrameImagePath = model.lastFrameImagePath,
            lastFrameCapturedAtEpochMillis = model.lastFrameCapturedAtEpochMillis,
            restoredAtEpochMillis = model.restoredAtEpochMillis,
        )
    }

    fun toConnectionHistoryState(entity: ConnectionHistoryStateEntity): ConnectionHistoryState {
        return ConnectionHistoryState(
            sourceId = entity.sourceId,
            previouslyConnected = entity.previouslyConnected,
            firstSuccessfulFrameAtEpochMillis = entity.firstSuccessfulFrameAtEpochMillis,
            lastSuccessfulFrameAtEpochMillis = entity.lastSuccessfulFrameAtEpochMillis,
        )
    }

    fun toConnectionHistoryEntity(model: ConnectionHistoryState): ConnectionHistoryStateEntity {
        return ConnectionHistoryStateEntity(
            sourceId = model.sourceId,
            previouslyConnected = model.previouslyConnected,
            firstSuccessfulFrameAtEpochMillis = model.firstSuccessfulFrameAtEpochMillis,
            lastSuccessfulFrameAtEpochMillis = model.lastSuccessfulFrameAtEpochMillis,
        )
    }
}
