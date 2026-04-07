package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.model.DiscoveryCompatibilityResult
import com.ndi.core.model.DiscoveryCompatibilitySnapshot
import com.ndi.core.model.DiscoveryCompatibilityStatus
import com.ndi.feature.ndibrowser.domain.repository.DiscoveryCompatibilityMatrixRepository
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock

class DiscoveryCompatibilityMatrixRepositoryImpl : DiscoveryCompatibilityMatrixRepository {

    private data class StoredResult(
        val result: DiscoveryCompatibilityResult,
        val recordedAtEpochMillis: Long,
    )

    private val mutex = Mutex()
    private val storedByTargetId = linkedMapOf<String, StoredResult>()
    private val matrixSnapshot = MutableStateFlow(
        DiscoveryCompatibilitySnapshot(
            recordedAtEpochMillis = 0L,
            results = emptyList(),
        ),
    )

    override fun observeMatrixSnapshot(): Flow<DiscoveryCompatibilitySnapshot> = matrixSnapshot.asStateFlow()

    override suspend fun upsertResults(
        results: List<DiscoveryCompatibilityResult>,
        recordedAtEpochMillis: Long,
    ) {
        if (results.isEmpty()) return
        mutex.withLock {
            val filtered = results.filter { it.status != DiscoveryCompatibilityStatus.PENDING }
            if (filtered.isEmpty()) return

            filtered.forEach { result ->
                val existing = storedByTargetId[result.targetId]
                if (existing == null || recordedAtEpochMillis >= existing.recordedAtEpochMillis) {
                    storedByTargetId[result.targetId] = StoredResult(result, recordedAtEpochMillis)
                }
            }

            val latest = storedByTargetId.values.maxOfOrNull { it.recordedAtEpochMillis } ?: recordedAtEpochMillis
            val orderedResults = storedByTargetId
                .values
                .sortedBy { it.result.targetId }
                .map { it.result }
            matrixSnapshot.value = DiscoveryCompatibilitySnapshot(
                recordedAtEpochMillis = latest,
                results = orderedResults,
            )
        }
    }

    override suspend fun getCurrentMatrix(): DiscoveryCompatibilitySnapshot = matrixSnapshot.value
}
