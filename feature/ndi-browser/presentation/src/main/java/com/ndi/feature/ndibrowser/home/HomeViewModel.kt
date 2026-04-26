package com.ndi.feature.ndibrowser.home

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.ndi.core.model.TelemetryEvent
import com.ndi.core.model.navigation.HomeDashboardSnapshot
import com.ndi.feature.ndibrowser.domain.repository.HomeDashboardRepository
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

sealed interface HomeNavigationEvent {
    object OpenStream : HomeNavigationEvent
    object OpenView : HomeNavigationEvent
}

data class HomeUiState(
    val snapshot: HomeDashboardSnapshot? = null,
    val isLoading: Boolean = false,
)

/**
 * ViewModel for the Home dashboard screen.
 * Read-only: renders aggregate status and emits navigation events for quick actions.
 */
class HomeViewModel(
    private val homeDashboardRepository: HomeDashboardRepository,
    private val telemetryEmitter: HomeTelemetryEmitter = HomeDependencies.telemetryEmitter,
) : ViewModel() {

    private val _uiState = MutableStateFlow(HomeUiState(isLoading = true))
    val uiState: StateFlow<HomeUiState> = _uiState.asStateFlow()

    private val _navigationEvents = MutableSharedFlow<HomeNavigationEvent>(extraBufferCapacity = 1)
    val navigationEvents: SharedFlow<HomeNavigationEvent> = _navigationEvents.asSharedFlow()

    init {
        viewModelScope.launch {
            homeDashboardRepository.observeDashboardSnapshot().collect { snapshot ->
                _uiState.update { it.copy(snapshot = snapshot, isLoading = false) }
            }
        }
    }

    fun onHomeVisible() {
        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true) }
            val snapshot = homeDashboardRepository.refreshDashboardSnapshot()
            _uiState.update { it.copy(snapshot = snapshot, isLoading = false) }
            telemetryEmitter.emit(
                TelemetryEvent(
                    name = TelemetryEvent.HOME_DASHBOARD_VIEWED,
                    timestampEpochMillis = System.currentTimeMillis(),
                ),
            )
        }
    }

    fun onOpenStreamActionPressed() {
        telemetryEmitter.emit(
            TelemetryEvent(
                name = TelemetryEvent.HOME_ACTION_OPEN_STREAM,
                timestampEpochMillis = System.currentTimeMillis(),
            ),
        )
        viewModelScope.launch { _navigationEvents.emit(HomeNavigationEvent.OpenStream) }
    }

    fun onOpenViewActionPressed() {
        val canNavigateToView = _uiState.value.snapshot?.canNavigateToView ?: false
        if (!canNavigateToView) {
            return
        }

        telemetryEmitter.emit(
            TelemetryEvent(
                name = TelemetryEvent.HOME_ACTION_OPEN_VIEW,
                timestampEpochMillis = System.currentTimeMillis(),
            ),
        )
        viewModelScope.launch { _navigationEvents.emit(HomeNavigationEvent.OpenView) }
    }

    class Factory(
        private val homeDashboardRepository: HomeDashboardRepository,
        private val telemetryEmitter: HomeTelemetryEmitter = HomeDependencies.telemetryEmitter,
    ) : ViewModelProvider.Factory {
        override fun <T : ViewModel> create(modelClass: Class<T>): T {
            require(modelClass.isAssignableFrom(HomeViewModel::class.java))
            @Suppress("UNCHECKED_CAST")
            return HomeViewModel(homeDashboardRepository, telemetryEmitter) as T
        }
    }
}

