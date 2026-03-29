package com.ndi.feature.ndibrowser.source_list

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.ndi.core.model.DiscoverySnapshot
import com.ndi.core.model.DiscoveryStatus
import com.ndi.core.model.DiscoveryTrigger
import com.ndi.core.model.NdiSource
import com.ndi.feature.ndibrowser.domain.repository.NdiDiscoveryRepository
import com.ndi.feature.ndibrowser.domain.repository.SourceAvailabilityStatus
import com.ndi.feature.ndibrowser.domain.repository.UserSelectionRepository
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

data class SourceListUiState(
    val discoveryStatus: DiscoveryStatus = DiscoveryStatus.EMPTY,
    val sources: List<NdiSource> = emptyList(),
    val highlightedSourceId: String? = null,
    val errorMessage: String? = null,
    val refreshErrorMessage: String? = null,
    val isRefreshing: Boolean = false,
    val fallbackWarning: String? = null,
    val layoutMode: SourceListLayoutMode = SourceListLayoutMode.COMPACT,
    val overlayDisplayState: com.ndi.feature.ndibrowser.settings.OverlayDisplayState? = null,
)

class SourceListViewModel(
    private val discoveryRepository: NdiDiscoveryRepository,
    private val userSelectionRepository: UserSelectionRepository,
    private val telemetryEmitter: SourceListTelemetryEmitter = SourceListDependencies.telemetryEmitter,
) : ViewModel() {

    companion object {
        private const val CURRENT_DEVICE_SOURCE_ID = "device-screen:local"
    }

    private val preselectionController = SourcePreselectionController(userSelectionRepository)

    private val _uiState = MutableStateFlow(SourceListUiState())
    val uiState: StateFlow<SourceListUiState> = _uiState.asStateFlow()

    private val _navigationEvents = MutableSharedFlow<String>(extraBufferCapacity = 1)
    val navigationEvents: SharedFlow<String> = _navigationEvents.asSharedFlow()

    private val _settingsToggleEvents = MutableSharedFlow<Unit>(extraBufferCapacity = 1)
    val settingsToggleEvents: SharedFlow<Unit> = _settingsToggleEvents.asSharedFlow()
    private var settingsToggleInFlight: Boolean = false

    // Track availability and connection history for UI enrichment
    private val availabilityHistory = MutableStateFlow<Map<String, SourceAvailabilityStatus>>(emptyMap())
    private val previouslyConnectedIds = MutableStateFlow<Set<String>>(emptySet())
    private var allPreviouslySources = mutableMapOf<String, NdiSource>()  // Preserve unavailable sources

    init {
        viewModelScope.launch {
            discoveryRepository.observeDiscoveryState().collect(::onDiscoverySnapshot)
        }
        viewModelScope.launch {
            discoveryRepository.observeAvailabilityHistory().collect { history ->
                availabilityHistory.value = history
                // Update UI state with enriched availability
                val enrichedSources = enrichSourcesWithAvailability()
                _uiState.update { current ->
                    current.copy(sources = enrichedSources)
                }
            }
        }
        viewModelScope.launch {
            SourceListDependencies.viewerContinuityRepositoryOrNull()?.observePreviouslyConnectedSourceIds()?.collect { ids ->
                previouslyConnectedIds.value = ids
                // Update UI state with enriched connection history
                val enrichedSources = enrichSourcesWithAvailability()
                _uiState.update { current ->
                    current.copy(sources = enrichedSources)
                }
            }
        }
    }

    fun onScreenVisible() {
        viewModelScope.launch {
            _uiState.update { current ->
                current.copy(highlightedSourceId = preselectionController.loadHighlightedSourceId())
            }
            discoveryRepository.startForegroundAutoRefresh(intervalSeconds = 5)
            discoveryRepository.discoverSources(DiscoveryTrigger.FOREGROUND_TICK)
        }
    }

    fun onScreenHidden() {
        discoveryRepository.stopForegroundAutoRefresh()
    }

    fun onManualRefresh() {
        refresh(trigger = DiscoveryTrigger.MANUAL)
    }

    fun onSourceSelected(sourceId: String) {
        // T036: Prevent navigation from unavailable rows
        val selectedSource = _uiState.value.sources.find { it.sourceId == sourceId }
        if (selectedSource != null && !selectedSource.isAvailable) {
            // Source is unavailable; emit blocked selection telemetry but do not navigate
            telemetryEmitter.emit(SourceListTelemetry.sourceSelectionBlocked(sourceId, "unavailable"))
            return
        }

        viewModelScope.launch {
            preselectionController.rememberSelection(sourceId)
            _uiState.update { current -> current.copy(highlightedSourceId = sourceId) }
            telemetryEmitter.emit(SourceListTelemetry.sourceSelected(sourceId))
            telemetryEmitter.emit(SourceListTelemetry.viewSelectionOpenedViewer(sourceId))
            _navigationEvents.emit(sourceId)
        }
    }

    fun onLayoutMeasured(widthDp: Int) {
        _uiState.update { current ->
            current.copy(layoutMode = SourceListAdaptiveLayout.resolve(widthDp))
        }
    }

    fun onSettingsTogglePressed() {
        if (settingsToggleInFlight) return
        settingsToggleInFlight = true
        _settingsToggleEvents.tryEmit(Unit)
    }

    fun onSettingsToggleSettled() {
        settingsToggleInFlight = false
    }

    fun onFallbackWarningChanged(message: String?) {
        _uiState.update { current -> current.copy(fallbackWarning = message) }
    }

    private fun refresh(trigger: DiscoveryTrigger) {
        viewModelScope.launch {
            discoveryRepository.discoverSources(trigger)
        }
    }

    private fun onDiscoverySnapshot(snapshot: DiscoverySnapshot) {
        _uiState.update {
            when (snapshot.status) {
                DiscoveryStatus.IN_PROGRESS -> {
                    // Keep the last visible list while refresh is running.
                    it.copy(
                        discoveryStatus = snapshot.status,
                        isRefreshing = true,
                        refreshErrorMessage = null,
                    )
                }

                DiscoveryStatus.SUCCESS,
                DiscoveryStatus.EMPTY,
                -> {
                    val filteredSources = filterViewableSources(snapshot.sources)
                    // Store newly seen sources for future reference
                    filteredSources.forEach { source ->
                        allPreviouslySources[source.sourceId] = source
                    }
                    // Combine newly seen sources with all previously seen sources to preserve unavailable ones
                    val allSourceIds = (filteredSources.map { it.sourceId } + allPreviouslySources.keys).toSet()
                    val combinedSources = allSourceIds.mapNotNull { sourceId ->
                        filteredSources.find { it.sourceId == sourceId }
                            ?: allPreviouslySources[sourceId]
                    }

                    val highlightedSourceId = it.highlightedSourceId?.takeIf { selected ->
                        combinedSources.any { source -> source.sourceId == selected }
                    }
                    it.copy(
                        discoveryStatus = snapshot.status,
                        sources = enrichSourcesWithAvailability(combinedSources),
                        highlightedSourceId = highlightedSourceId,
                        errorMessage = snapshot.errorMessage,
                        refreshErrorMessage = null,
                        isRefreshing = false,
                    )
                }

                DiscoveryStatus.FAILURE -> {
                    // Preserve the last visible list and show non-blocking inline refresh feedback.
                    it.copy(
                        discoveryStatus = snapshot.status,
                        errorMessage = snapshot.errorMessage,
                        refreshErrorMessage = snapshot.errorMessage,
                        isRefreshing = false,
                    )
                }
            }
        }
        telemetryEmitter.emit(SourceListTelemetry.fromSnapshot(snapshot))
    }

    private fun enrichSourcesWithAvailability(sources: List<NdiSource>? = null): List<NdiSource> {
        val sourcesToEnrich = sources ?: _uiState.value.sources
        val availability = availabilityHistory.value
        val previouslyConnected = previouslyConnectedIds.value

        return sourcesToEnrich.map { source ->
            val isAvailable = availability[source.sourceId]?.isAvailable ?: true
            val wasPreviouslyConnected = source.sourceId in previouslyConnected

            source.copy(
                isAvailable = isAvailable,
                previouslyConnected = wasPreviouslyConnected,
            )
        }
    }

    private fun filterViewableSources(sources: List<NdiSource>): List<NdiSource> {
        return sources.filterNot(::isCurrentDeviceSource)
    }

    private fun isCurrentDeviceSource(source: NdiSource): Boolean {
        return source.sourceId == CURRENT_DEVICE_SOURCE_ID
    }

    class Factory(
        private val discoveryRepository: NdiDiscoveryRepository,
        private val userSelectionRepository: UserSelectionRepository,
        private val telemetryEmitter: SourceListTelemetryEmitter = SourceListDependencies.telemetryEmitter,
    ) : ViewModelProvider.Factory {
        override fun <T : ViewModel> create(modelClass: Class<T>): T {
            require(modelClass.isAssignableFrom(SourceListViewModel::class.java))
            return SourceListViewModel(discoveryRepository, userSelectionRepository, telemetryEmitter) as T
        }
    }
}
