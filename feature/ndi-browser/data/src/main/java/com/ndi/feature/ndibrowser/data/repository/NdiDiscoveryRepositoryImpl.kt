package com.ndi.feature.ndibrowser.data.repository

import android.util.Log
import com.ndi.core.database.UserSelectionDao
import com.ndi.core.database.DiscoveryRunResultDao
import com.ndi.core.database.DiscoveryServerDiagnosticRecordDao
import com.ndi.core.model.DiscoverySnapshot
import com.ndi.core.model.DiscoveryStatus
import com.ndi.core.model.CachedSourceRecord
import com.ndi.core.model.CachedSourceValidationState
import com.ndi.core.model.DiscoveryRunResult
import com.ndi.core.model.DiscoveryRunStatus
import com.ndi.core.model.DiscoveryServerAttemptStatus
import com.ndi.core.model.DiscoveryServerDiagnosticRecord
import com.ndi.core.model.DiscoveryCompatibilityResult
import com.ndi.core.model.DiscoveryCompatibilitySnapshot
import com.ndi.core.model.DiscoveryCompatibilityStatus
import com.ndi.core.model.DiscoveryTrigger
import com.ndi.core.model.DiscoveryMode
import com.ndi.core.model.NdiLogCategory
import com.ndi.core.model.NdiLogLevel
import com.ndi.feature.ndibrowser.data.AvailabilityDebounceTracker
import com.ndi.feature.ndibrowser.data.DiscoveryRefreshCoordinator
import com.ndi.feature.ndibrowser.data.mapper.NdiSourceMapper
import com.ndi.feature.ndibrowser.domain.repository.DiscoveryCompatibilityMatrixRepository
import com.ndi.feature.ndibrowser.domain.repository.CachedSourceRepository
import com.ndi.feature.ndibrowser.domain.repository.NdiDiscoveryConfigRepository
import com.ndi.feature.ndibrowser.domain.repository.NdiDiscoveryRepository
import com.ndi.feature.ndibrowser.domain.repository.SourceAvailabilityStatus
import com.ndi.sdkbridge.NdiDiscoveryBridge
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.async
import kotlinx.coroutines.awaitAll
import kotlinx.coroutines.coroutineScope
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.withContext
import kotlinx.coroutines.withTimeoutOrNull
import java.util.UUID

class NdiDiscoveryRepositoryImpl(
    private val bridge: NdiDiscoveryBridge,
    private val userSelectionDao: UserSelectionDao,
    private val discoveryConfigRepository: NdiDiscoveryConfigRepository,
    private val scope: CoroutineScope = CoroutineScope(SupervisorJob() + Dispatchers.IO),
    private val sourceMapper: NdiSourceMapper = NdiSourceMapper(),
    private val refreshCoordinator: DiscoveryRefreshCoordinator = DiscoveryRefreshCoordinator(scope),
    private val availabilityTracker: AvailabilityDebounceTracker = AvailabilityDebounceTracker(),
    private val diagnosticsLogBuffer: DeveloperDiagnosticsLogBuffer? = null,
    private val compatibilityClassifier: DiscoveryCompatibilityClassifier = DiscoveryCompatibilityClassifier(),
    private val compatibilityMatrixRepository: DiscoveryCompatibilityMatrixRepository = DiscoveryCompatibilityMatrixRepositoryImpl(),
    private val cachedSourceRepository: CachedSourceRepository? = null,
    private val cachedSourceIdentityResolver: CachedSourceIdentityResolver = CachedSourceIdentityResolver(),
    // ---- T010: Discovery run result and diagnostics DAOs ----
    private val discoveryRunResultDao: DiscoveryRunResultDao? = null,
    private val discoveryServerDiagnosticRecordDao: DiscoveryServerDiagnosticRecordDao? = null,
) : NdiDiscoveryRepository {

    private class DiscoveryTimeoutException(message: String) : IllegalStateException(message)

    private companion object {
        const val LOCAL_SCREEN_SOURCE_ID = "device-screen:local"
        const val LOCAL_SCREEN_DISPLAY_NAME = "This Device Screen"
        const val TAG = "NdiDiscoveryRepo"
        const val MAX_DISCOVERY_SERVER_RETRIES = 3
    }

    private data class DiscoveryServerProbeResult(
        val endpoint: com.ndi.core.model.NdiDiscoveryEndpoint,
        val reachable: Boolean,
        val attempts: Int,
        val temporarilyDisabled: Boolean,
    )

    private val discoveryState = MutableStateFlow(emptySnapshot())
    private val compatibilityState = MutableStateFlow(
        DiscoveryCompatibilitySnapshot(
            recordedAtEpochMillis = 0L,
            results = emptyList(),
        ),
    )
    private val discoveryServerHealthLock = Any()
    private val temporarilyDisabledDiscoveryServers = mutableSetOf<String>()
    private val availabilityHistory = MutableStateFlow<Map<String, SourceAvailabilityStatus>>(emptyMap())
    private val latestRunResultState = MutableStateFlow<DiscoveryRunResult?>(null)

    override suspend fun discoverSources(trigger: DiscoveryTrigger): DiscoverySnapshot {
        val startedAt = System.currentTimeMillis()
        var selectedMode = DiscoveryMode.MULTICAST
        var selectedRunId = UUID.randomUUID().toString()
        var lastServerProbeResults: List<DiscoveryServerProbeResult> = emptyList()
        discoveryState.value = discoveryState.value.copy(
            startedAtEpochMillis = startedAt,
            status = DiscoveryStatus.IN_PROGRESS,
        )

        return runCatching {
            // ---- T016: Per-run multicast selection (US1 Phase 3) ----
            // Select discovery mode based on enabled server count
            val enabledServerCount = discoveryConfigRepository.getEnabledServerCount()
            val modeSnapshot = selectDiscoveryMode(enabledServerCount)
            selectedMode = modeSnapshot.mode
            selectedRunId = modeSnapshot.runId

            val endpoints = if (modeSnapshot.mode == com.ndi.core.model.DiscoveryMode.MULTICAST) {
                emptyList()
            } else {
                discoveryConfigRepository.getEnabledServersSnapshot()
            }

            val serverProbeResults = if (modeSnapshot.mode == DiscoveryMode.DISCOVERY_SERVER) {
                probeDiscoveryServersInParallel(endpoints)
            } else {
                emptyList()
            }
            lastServerProbeResults = serverProbeResults

            val endpointReachability = serverProbeResults.associate { result ->
                result.endpoint to result.reachable
            }
            val temporarilyDisabledCount = serverProbeResults.count { it.temporarilyDisabled }
            val discoveryEndpoints = if (modeSnapshot.mode == DiscoveryMode.DISCOVERY_SERVER) {
                serverProbeResults
                    .filter { it.reachable && !it.temporarilyDisabled }
                    .map { it.endpoint }
            } else {
                endpoints
            }

            runCatching { Log.d(TAG, "Discovery trigger=$trigger, mode=${modeSnapshot.mode}, enabledServers=$enabledServerCount") }
            diagnosticsLogBuffer?.appendLog(
                category = NdiLogCategory.DISCOVERY,
                level = NdiLogLevel.INFO,
                message = "discovery_refresh_started trigger=${trigger.name} mode=${modeSnapshot.mode} reason=${modeSnapshot.modeSelectionReason}",
            )

            // Set ALL eligible endpoints at once. The NDI SDK (v5+) supports a
            // comma-separated list in NDI_DISCOVERY_SERVER and contacts all servers
            // simultaneously via a single finder instance. Calling setDiscoveryEndpoints
            // with one entry at a time inside a loop would trigger NDI reinit per
            // iteration, discarding the accumulated source list on every tick.
            withContext(Dispatchers.IO) {
                bridge.setDiscoveryEndpoints(discoveryEndpoints)
            }

            diagnosticsLogBuffer?.appendLog(
                category = NdiLogCategory.DISCOVERY,
                level = NdiLogLevel.INFO,
                message = if (discoveryEndpoints.isEmpty()) {
                    "discovery using default network discovery (no custom servers configured)"
                } else {
                    "discovery via ${discoveryEndpoints.joinToString { "${it.host}:${it.resolvedPort}" }}"
                },
            )

            if (endpoints.isNotEmpty()) {
                val reachableEndpoints = endpointReachability.values.count { it }
                val unreachableEndpoints = endpoints.size - reachableEndpoints
                serverProbeResults.forEach { result ->
                    diagnosticsLogBuffer?.appendLog(
                        category = NdiLogCategory.DISCOVERY,
                        level = if (result.reachable) NdiLogLevel.INFO else NdiLogLevel.WARN,
                        message = "server ${result.endpoint.host}:${result.endpoint.resolvedPort} reachable=${result.reachable} attempts=${result.attempts} disabled=${result.temporarilyDisabled}",
                    )
                }
                if (unreachableEndpoints > 0) {
                    diagnosticsLogBuffer?.appendLog(
                        category = NdiLogCategory.DISCOVERY,
                        level = NdiLogLevel.WARN,
                        message = "discovery endpoint reachability partial: reachable=$reachableEndpoints unreachable=$unreachableEndpoints total=${endpoints.size}",
                    )
                }
                if (temporarilyDisabledCount > 0) {
                    serverProbeResults
                        .filter { it.temporarilyDisabled }
                        .forEach { result ->
                            diagnosticsLogBuffer?.appendLog(
                                category = NdiLogCategory.DISCOVERY,
                                level = NdiLogLevel.WARN,
                                message = "server ${result.endpoint.host}:${result.endpoint.resolvedPort} temporarily disabled after $MAX_DISCOVERY_SERVER_RETRIES failed retries (until next app start)",
                            )
                        }
                }
            }

            val discoveredRaw = withContext(Dispatchers.IO) {
                if (modeSnapshot.mode == DiscoveryMode.DISCOVERY_SERVER) {
                    if (discoveryEndpoints.isEmpty()) {
                        emptyList()
                    } else {
                    withTimeoutOrNull(5_000L) {
                        sourceMapper.map(bridge.discoverSources())
                    } ?: throw DiscoveryTimeoutException(
                        buildString {
                            append("discovery server timeout after 5000ms")
                            if (discoveryEndpoints.isNotEmpty()) {
                                append(" endpoint=")
                                append(discoveryEndpoints.joinToString { "${it.host}:${it.resolvedPort}" })
                            }
                            append(" timestamp=")
                            append(System.currentTimeMillis())
                        },
                    )
                    }
                } else {
                    sourceMapper.map(bridge.discoverSources())
                }
            }

            val discovered = if (modeSnapshot.mode == DiscoveryMode.DISCOVERY_SERVER) {
                val (valid, invalid) = discoveredRaw.partition { parseEndpoint(it.endpointAddress) != null }
                if (invalid.isNotEmpty()) {
                    diagnosticsLogBuffer?.appendLog(
                        category = NdiLogCategory.DISCOVERY,
                        level = NdiLogLevel.WARN,
                        message = "filtered_invalid_discovery_records count=${invalid.size}",
                    )
                }
                valid
            } else {
                discoveredRaw
            }

            val deduplicatedDiscovered = discovered.distinctBy { it.sourceId }
            val endpointCompatibilityResults = if (endpoints.isEmpty()) {
                listOf(
                    DiscoveryCompatibilityResult(
                        targetId = "default-network",
                        status = compatibilityClassifier.classify(
                            discoverySucceeded = deduplicatedDiscovered.isNotEmpty(),
                            streamStartAttempted = false,
                            streamStartSucceeded = false,
                            blocked = false,
                        ),
                        discoveredSourceCount = deduplicatedDiscovered.size,
                        streamStartAttempted = false,
                        streamStartSucceeded = false,
                        temporaryUnknownObserved = false,
                        notes = "Derived from discovery-only pre-stream checks",
                    ),
                )
            } else {
                endpoints.map { endpoint ->
                    val reachable = endpointReachability[endpoint] == true
                    val endpointDiscoverySucceeded = reachable && deduplicatedDiscovered.isNotEmpty()
                    DiscoveryCompatibilityResult(
                        targetId = "${endpoint.host}:${endpoint.resolvedPort}",
                        status = compatibilityClassifier.classify(
                            discoverySucceeded = endpointDiscoverySucceeded,
                            streamStartAttempted = false,
                            streamStartSucceeded = false,
                            blocked = !reachable,
                        ),
                        discoveredSourceCount = if (reachable) deduplicatedDiscovered.size else 0,
                        streamStartAttempted = false,
                        streamStartSucceeded = false,
                        temporaryUnknownObserved = false,
                        notes = if (reachable) {
                            "Endpoint reachable; discovery verified in pre-stream phase"
                        } else {
                            "Endpoint unreachable during discovery probe"
                        },
                    )
                }
            }
            val overallStatus = deriveOverallCompatibilityStatus(endpointCompatibilityResults)
            val hasNonCompatible = endpointCompatibilityResults.any {
                it.status == DiscoveryCompatibilityStatus.BLOCKED ||
                    it.status == DiscoveryCompatibilityStatus.INCOMPATIBLE
            }
            val hasUsable = endpointCompatibilityResults.any {
                it.status == DiscoveryCompatibilityStatus.COMPATIBLE ||
                    it.status == DiscoveryCompatibilityStatus.LIMITED
            }
            val overallCompatibilityResults = if (endpoints.isEmpty()) {
                endpointCompatibilityResults
            } else {
                endpointCompatibilityResults + DiscoveryCompatibilityResult(
                    targetId = "configured-endpoints-overall",
                    status = overallStatus,
                    discoveredSourceCount = deduplicatedDiscovered.size,
                    streamStartAttempted = false,
                    streamStartSucceeded = false,
                    temporaryUnknownObserved = false,
                    notes = if (hasNonCompatible && hasUsable) {
                        "Partial compatibility: usable sources discovered while at least one endpoint is non-compatible"
                    } else {
                        "Aggregated from configured endpoint compatibility checks"
                    },
                )
            }
            compatibilityState.value = DiscoveryCompatibilitySnapshot(
                recordedAtEpochMillis = System.currentTimeMillis(),
                results = overallCompatibilityResults,
            )

            if (modeSnapshot.mode == DiscoveryMode.DISCOVERY_SERVER && endpoints.isNotEmpty()) {
                val serverDiagnostics = endpoints.map { endpoint ->
                    val endpointId = "${endpoint.host}:${endpoint.resolvedPort}"
                    val reachable = endpointReachability[endpoint] == true
                    DiscoveryServerDiagnosticRecord(
                        runId = modeSnapshot.runId,
                        serverId = endpointId,
                        endpoint = endpointId,
                        attemptStartedAtEpochMillis = startedAt,
                        durationMillis = (System.currentTimeMillis() - startedAt).coerceAtLeast(0L),
                        status = if (reachable) DiscoveryServerAttemptStatus.SUCCESS else DiscoveryServerAttemptStatus.UNREACHABLE,
                        errorDetail = if (reachable) null else "endpoint unreachable after $MAX_DISCOVERY_SERVER_RETRIES retries",
                    )
                }
                serverDiagnostics.forEach { recordServerDiagnostics(it) }
            }

            recordDiscoveryRunResult(
                DiscoveryRunResult(
                    runId = modeSnapshot.runId,
                    mode = modeSnapshot.mode,
                    durationMillis = (System.currentTimeMillis() - startedAt).coerceAtLeast(0L),
                    status = DiscoveryRunStatus.SUCCESS,
                    sourceCount = deduplicatedDiscovered.size,
                    diagnosticCode = null,
                    diagnosticMessage = null,
                ),
            )
            compatibilityMatrixRepository.upsertResults(
                results = overallCompatibilityResults,
                recordedAtEpochMillis = compatibilityState.value.recordedAtEpochMillis,
            )

            diagnosticsLogBuffer?.appendLog(
                category = NdiLogCategory.DISCOVERY,
                level = NdiLogLevel.INFO,
                message = "discovery returned ${deduplicatedDiscovered.size} sources",
            )

            val nowEpochMillis = System.currentTimeMillis()
            deduplicatedDiscovered
                .filter { it.sourceId != LOCAL_SCREEN_SOURCE_ID }
                .forEach { source ->
                    // Non-fatal: persistence errors must not affect discovery return value.
                    runCatching { persistDiscoveredSource(source, nowEpochMillis) }.onFailure { e ->
                        diagnosticsLogBuffer?.appendLog(
                            category = NdiLogCategory.DISCOVERY,
                            level = NdiLogLevel.WARN,
                            message = "cached_source_persist_failed source=${source.sourceId}: ${e.message}",
                        )
                    }
                }

            userSelectionDao.getSelection()
            val sources = deduplicatedDiscovered
                .toMutableList().apply {
                add(
                    0,
                    com.ndi.core.model.NdiSource(
                        sourceId = LOCAL_SCREEN_SOURCE_ID,
                        displayName = LOCAL_SCREEN_DISPLAY_NAME,
                        endpointAddress = null,
                        isReachable = true,
                        lastSeenAtEpochMillis = System.currentTimeMillis(),
                    ),
                )
            }
            val completedAt = System.currentTimeMillis()
            val status = if (deduplicatedDiscovered.isEmpty()) DiscoveryStatus.EMPTY else DiscoveryStatus.SUCCESS
            DiscoverySnapshot(
                snapshotId = UUID.randomUUID().toString(),
                startedAtEpochMillis = startedAt,
                completedAtEpochMillis = completedAt,
                status = status,
                sourceCount = sources.size,
                sources = sources,
            ).also {
                diagnosticsLogBuffer?.appendLog(
                    category = NdiLogCategory.DISCOVERY,
                    level = NdiLogLevel.INFO,
                    message = "discovery_refresh_completed status=${status.name} sourceCount=${deduplicatedDiscovered.size} totalWithLocal=${sources.size}",
                )
            }
        }.onFailure { e ->
            // Restore cooperative cancellation — runCatching catches CancellationException,
            // which would prevent the coroutine from honouring its cancellation signal.
            if (e is CancellationException) throw e
        }.getOrElse { error ->
            diagnosticsLogBuffer?.appendLog(
                category = NdiLogCategory.DISCOVERY,
                level = NdiLogLevel.ERROR,
                message = "discovery failed: ${error.message ?: "unknown error"}",
            )
            val timeoutFailure = selectedMode == DiscoveryMode.DISCOVERY_SERVER &&
                (error is DiscoveryTimeoutException || (error.message?.contains("timeout", ignoreCase = true) == true))
            val errorCode = if (timeoutFailure) "DISCOVERY_SERVER_TIMEOUT" else trigger.name
            val enrichedMessage = if (timeoutFailure) {
                val timeoutTargets = lastServerProbeResults
                    .filter { it.reachable && !it.temporarilyDisabled }
                    .map { "${it.endpoint.host}:${it.endpoint.resolvedPort}" }
                val configuredEndpointText = timeoutTargets
                    .ifEmpty {
                        discoveryConfigRepository.getEnabledServersSnapshot()
                            .map { "${it.host}:${it.resolvedPort}" }
                    }
                    .joinToString()
                    .ifBlank { "none" }
                "${error.message ?: "discovery server timeout"}; endpoint=$configuredEndpointText; timestamp=${System.currentTimeMillis()}"
            } else {
                error.message
            }

            if (timeoutFailure) {
                val timeoutRunId = selectedRunId
                recordDiscoveryRunResult(
                    DiscoveryRunResult(
                        runId = timeoutRunId,
                        mode = DiscoveryMode.DISCOVERY_SERVER,
                        durationMillis = (System.currentTimeMillis() - startedAt).coerceAtLeast(0L),
                        status = DiscoveryRunStatus.TIMEOUT,
                        sourceCount = 0,
                        diagnosticCode = errorCode,
                        diagnosticMessage = enrichedMessage,
                    ),
                )
                val diagnosticsTargets = lastServerProbeResults.ifEmpty {
                    discoveryConfigRepository.getEnabledServersSnapshot().map { endpoint ->
                        DiscoveryServerProbeResult(
                            endpoint = endpoint,
                            reachable = true,
                            attempts = MAX_DISCOVERY_SERVER_RETRIES,
                            temporarilyDisabled = false,
                        )
                    }
                }
                diagnosticsTargets.forEach { probe ->
                    val endpointId = "${probe.endpoint.host}:${probe.endpoint.resolvedPort}"
                    val status = if (probe.reachable && !probe.temporarilyDisabled) {
                        DiscoveryServerAttemptStatus.TIMEOUT
                    } else {
                        DiscoveryServerAttemptStatus.UNREACHABLE
                    }
                    recordServerDiagnostics(
                        DiscoveryServerDiagnosticRecord(
                            runId = timeoutRunId,
                            serverId = endpointId,
                            endpoint = endpointId,
                            attemptStartedAtEpochMillis = startedAt,
                            durationMillis = (System.currentTimeMillis() - startedAt).coerceAtLeast(5_000L),
                            status = status,
                            errorDetail = if (status == DiscoveryServerAttemptStatus.TIMEOUT) {
                                enrichedMessage
                            } else {
                                "endpoint unreachable after $MAX_DISCOVERY_SERVER_RETRIES retries"
                            },
                        ),
                    )
                }
            } else {
                recordDiscoveryRunResult(
                    DiscoveryRunResult(
                        runId = UUID.randomUUID().toString(),
                        mode = selectedMode,
                        durationMillis = (System.currentTimeMillis() - startedAt).coerceAtLeast(0L),
                        status = DiscoveryRunStatus.FAILURE,
                        sourceCount = 0,
                        diagnosticCode = errorCode,
                        diagnosticMessage = enrichedMessage,
                    ),
                )
            }

            DiscoverySnapshot(
                snapshotId = UUID.randomUUID().toString(),
                startedAtEpochMillis = startedAt,
                completedAtEpochMillis = System.currentTimeMillis(),
                status = DiscoveryStatus.FAILURE,
                sourceCount = 0,
                sources = emptyList(),
                errorCode = errorCode,
                errorMessage = enrichedMessage,
            )
        }.also { snapshot ->
            if (snapshot.status != DiscoveryStatus.FAILURE) {
                val discoveredSourceCount = snapshot.sources.count { it.sourceId != LOCAL_SCREEN_SOURCE_ID }
                diagnosticsLogBuffer?.appendLog(
                    category = NdiLogCategory.DISCOVERY,
                    level = NdiLogLevel.INFO,
                    message = "discovery ${snapshot.status.name.lowercase()} with $discoveredSourceCount discovered sources",
                )
            }
            discoveryState.value = snapshot
            // Update availability history based on the new snapshot
            updateAvailabilityHistory(snapshot)
        }
    }

    private suspend fun probeDiscoveryServersInParallel(
        endpoints: List<com.ndi.core.model.NdiDiscoveryEndpoint>,
    ): List<DiscoveryServerProbeResult> = coroutineScope {
        endpoints.map { endpoint ->
            async(Dispatchers.IO) {
                val endpointId = "${endpoint.host.trim().lowercase()}:${endpoint.resolvedPort}"
                val alreadyDisabled = synchronized(discoveryServerHealthLock) {
                    temporarilyDisabledDiscoveryServers.contains(endpointId)
                }
                if (alreadyDisabled) {
                    return@async DiscoveryServerProbeResult(
                        endpoint = endpoint,
                        reachable = false,
                        attempts = 0,
                        temporarilyDisabled = true,
                    )
                }

                var attempts = 0
                var reachable = false
                while (attempts < MAX_DISCOVERY_SERVER_RETRIES && !reachable) {
                    attempts += 1
                    reachable = runCatching {
                        bridge.isDiscoveryServerReachable(endpoint.host, endpoint.resolvedPort)
                    }.getOrDefault(false)
                }

                if (reachable) {
                } else {
                    synchronized(discoveryServerHealthLock) {
                        if (attempts >= MAX_DISCOVERY_SERVER_RETRIES) {
                            temporarilyDisabledDiscoveryServers.add(endpointId)
                        }
                    }
                }

                val disabledAfterProbe = synchronized(discoveryServerHealthLock) {
                    temporarilyDisabledDiscoveryServers.contains(endpointId)
                }

                DiscoveryServerProbeResult(
                    endpoint = endpoint,
                    reachable = reachable,
                    attempts = attempts,
                    temporarilyDisabled = disabledAfterProbe,
                )
            }
        }.awaitAll()
    }

    private fun updateAvailabilityHistory(snapshot: DiscoverySnapshot) {
        val nowEpochMillis = System.currentTimeMillis()
        val seenSourceIds = snapshot.sources.map { it.sourceId }.toSet()
        val currentHistory = availabilityHistory.value.toMutableMap()

        // Update or initialize status for seen sources
        for (source in snapshot.sources) {
            val previous = currentHistory[source.sourceId]
            val updated = availabilityTracker.update(
                previous = previous,
                sourceId = source.sourceId,
                seenInSnapshot = true,
                nowEpochMillis = nowEpochMillis,
            )
            currentHistory[source.sourceId] = updated
        }

        // Mark previously seen sources as missing if not in current snapshot
        for ((sourceId, previousStatus) in currentHistory) {
            if (sourceId !in seenSourceIds && sourceId != LOCAL_SCREEN_SOURCE_ID) {
                val updated = availabilityTracker.update(
                    previous = previousStatus,
                    sourceId = sourceId,
                    seenInSnapshot = false,
                    nowEpochMillis = nowEpochMillis,
                )
                currentHistory[sourceId] = updated
            }
        }

        availabilityHistory.value = currentHistory
    }

    override fun observeDiscoveryState(): Flow<DiscoverySnapshot> = discoveryState.asStateFlow()

    override fun observeCompatibilitySnapshot(): Flow<DiscoveryCompatibilitySnapshot> =
        compatibilityState.asStateFlow()

    override fun startForegroundAutoRefresh(intervalSeconds: Int) {
        refreshCoordinator.start(intervalSeconds) {
            discoverSources(DiscoveryTrigger.FOREGROUND_TICK)
        }
    }

    override fun stopForegroundAutoRefresh() {
        refreshCoordinator.stop()
    }

    override fun observeAvailabilityHistory(): Flow<Map<String, SourceAvailabilityStatus>> {
        return availabilityHistory.asStateFlow()
    }

    override suspend fun getSourceAvailabilityStatus(sourceId: String): SourceAvailabilityStatus? {
        return availabilityHistory.value[sourceId]
    }

    private fun emptySnapshot(): DiscoverySnapshot {
        return DiscoverySnapshot(
            snapshotId = UUID.randomUUID().toString(),
            startedAtEpochMillis = 0L,
            completedAtEpochMillis = 0L,
            status = DiscoveryStatus.EMPTY,
            sourceCount = 0,
            sources = emptyList(),
        )
    }

    private fun deriveOverallCompatibilityStatus(
        endpointResults: List<DiscoveryCompatibilityResult>,
    ): DiscoveryCompatibilityStatus {
        if (endpointResults.isEmpty()) return DiscoveryCompatibilityStatus.PENDING
        val hasIncompatible = endpointResults.any { it.status == DiscoveryCompatibilityStatus.INCOMPATIBLE }
        if (hasIncompatible) return DiscoveryCompatibilityStatus.INCOMPATIBLE

        val hasBlocked = endpointResults.any { it.status == DiscoveryCompatibilityStatus.BLOCKED }
        val hasUsable = endpointResults.any {
            it.status == DiscoveryCompatibilityStatus.COMPATIBLE ||
                it.status == DiscoveryCompatibilityStatus.LIMITED
        }
        if (hasBlocked && hasUsable) return DiscoveryCompatibilityStatus.LIMITED
        if (hasBlocked) return DiscoveryCompatibilityStatus.BLOCKED
        if (hasUsable) return DiscoveryCompatibilityStatus.LIMITED
        return DiscoveryCompatibilityStatus.PENDING
    }

    private suspend fun persistDiscoveredSource(
        source: com.ndi.core.model.NdiSource,
        nowEpochMillis: Long,
    ) {
        // Sources discovered via native NDI SDK (mDNS/multicast) have no endpointAddress;
        // use a synthetic host derived from the sourceId so they are still persisted.
        val parsed = parseEndpoint(source.endpointAddress)
        val endpointHost = parsed?.first ?: source.sourceId.trim().lowercase()
        val endpointPort = parsed?.second ?: 0
        val cacheKey = cachedSourceIdentityResolver.buildCacheKey(source)

        cachedSourceRepository?.upsertFromDiscovery(
            CachedSourceRecord(
                cacheKey = cacheKey,
                stableSourceId = source.sourceId.takeIf { it.isNotBlank() },
                lastObservedSourceId = source.sourceId.takeIf { it.isNotBlank() },
                displayName = source.displayName,
                endpointHost = endpointHost,
                endpointPort = endpointPort,
                endpointKey = "$endpointHost:$endpointPort",
                validationState = if (source.isAvailable) {
                    CachedSourceValidationState.AVAILABLE
                } else {
                    CachedSourceValidationState.UNAVAILABLE
                },
                lastAvailableAtEpochMillis = if (source.isAvailable) nowEpochMillis else null,
                lastValidatedAtEpochMillis = nowEpochMillis,
                lastValidationStartedAtEpochMillis = nowEpochMillis,
                firstCachedAtEpochMillis = nowEpochMillis,
                lastDiscoveredAtEpochMillis = nowEpochMillis,
                retainedPreviewImagePath = source.lastFramePreviewPath,
                lastPreviewCapturedAtEpochMillis = source.lastSeenAtEpochMillis,
                updatedAtEpochMillis = nowEpochMillis,
            ),
        )
    }

    private fun parseEndpoint(endpointAddress: String?): Pair<String, Int>? {
        val value = endpointAddress?.trim()?.takeIf { it.isNotBlank() } ?: return null
        val splitIndex = value.lastIndexOf(':')
        if (splitIndex <= 0 || splitIndex == value.lastIndex) return null

        val host = value.substring(0, splitIndex).trim().lowercase()
        val port = value.substring(splitIndex + 1).trim().toIntOrNull() ?: return null
        if (host.isBlank() || port !in 1..65535) return null
        return host to port
    }

    override fun observeLatestRunResult(): Flow<DiscoveryRunResult?> = latestRunResultState.asStateFlow()

    override suspend fun recordDiscoveryRunResult(result: DiscoveryRunResult) {
        latestRunResultState.value = result
        discoveryRunResultDao?.upsert(
            com.ndi.core.database.DiscoveryRunResultEntity(
                runId = result.runId,
                mode = result.mode.name,
                durationMillis = result.durationMillis,
                status = result.status.name,
                sourceCount = result.sourceCount,
                diagnosticCode = result.diagnosticCode,
                diagnosticMessage = result.diagnosticMessage,
                recordedAtEpochMillis = System.currentTimeMillis(),
            ),
        )
    }

    override suspend fun recordServerDiagnostics(diagnostics: DiscoveryServerDiagnosticRecord) {
        discoveryServerDiagnosticRecordDao?.upsert(
            com.ndi.core.database.DiscoveryServerDiagnosticRecordEntity(
                runId = diagnostics.runId,
                serverId = diagnostics.serverId,
                endpoint = diagnostics.endpoint,
                attemptStartedAtEpochMillis = diagnostics.attemptStartedAtEpochMillis,
                durationMillis = diagnostics.durationMillis,
                status = diagnostics.status.name,
                errorDetail = diagnostics.errorDetail,
                recordedAtEpochMillis = System.currentTimeMillis(),
            ),
        )
    }
}
