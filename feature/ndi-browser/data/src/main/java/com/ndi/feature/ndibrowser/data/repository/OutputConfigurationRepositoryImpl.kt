package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.database.OutputConfigurationDao
import com.ndi.core.database.OutputConfigurationEntity
import com.ndi.core.model.OutputConfiguration
import com.ndi.core.model.OutputInputKind
import com.ndi.feature.ndibrowser.domain.repository.OutputConfigurationRepository

class OutputConfigurationRepositoryImpl(
    private val outputConfigurationDao: OutputConfigurationDao,
) : OutputConfigurationRepository {

    private val defaultStreamName = "NDI Output"

    override suspend fun savePreferredStreamName(value: String) {
        val normalized = value.trim().ifBlank { defaultStreamName }
        val current = outputConfigurationDao.get() ?: defaultEntity()
        outputConfigurationDao.upsert(
            current.copy(
                preferredStreamName = normalized,
                updatedAtEpochMillis = System.currentTimeMillis(),
            ),
        )
    }

    override suspend fun getPreferredStreamName(): String {
        return outputConfigurationDao.get()?.preferredStreamName ?: defaultStreamName
    }

    override suspend fun saveLastSelectedInputSource(sourceId: String) {
        val normalizedSourceId = sourceId.trim().ifBlank { return }
        val current = outputConfigurationDao.get() ?: defaultEntity()
        outputConfigurationDao.upsert(
            current.copy(
                lastSelectedInputSourceId = normalizedSourceId,
                lastSelectedInputSourceKind = if (normalizedSourceId.startsWith("device-screen:")) {
                    OutputInputKind.DEVICE_SCREEN.name
                } else {
                    OutputInputKind.DISCOVERED_NDI.name
                },
                updatedAtEpochMillis = System.currentTimeMillis(),
            ),
        )
    }

    override suspend fun getLastSelectedInputSource(): String? {
        return outputConfigurationDao.get()?.lastSelectedInputSourceId
    }

    override suspend fun getConfiguration(): OutputConfiguration {
        return (outputConfigurationDao.get() ?: defaultEntity()).toModel()
    }

    private fun defaultEntity(): OutputConfigurationEntity {
        return OutputConfigurationEntity(
            preferredStreamName = defaultStreamName,
            lastSelectedInputSourceId = null,
            lastSelectedInputSourceKind = null,
            autoRetryEnabled = true,
            retryWindowSeconds = 15,
            updatedAtEpochMillis = System.currentTimeMillis(),
        )
    }

    private fun OutputConfigurationEntity.toModel(): OutputConfiguration {
        return OutputConfiguration(
            preferredStreamName = preferredStreamName,
            lastSelectedInputSourceId = lastSelectedInputSourceId,
            lastSelectedInputSourceKind = lastSelectedInputSourceKind?.let(OutputInputKind::valueOf),
            autoRetryEnabled = autoRetryEnabled,
            retryWindowSeconds = retryWindowSeconds,
        )
    }
}
