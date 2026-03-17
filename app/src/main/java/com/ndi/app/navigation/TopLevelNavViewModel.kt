package com.ndi.app.navigation

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.ndi.core.model.navigation.LaunchContext
import com.ndi.core.model.navigation.NavigationLayoutProfile
import com.ndi.core.model.navigation.NavigationTrigger
import com.ndi.core.model.navigation.TopLevelDestination
import com.ndi.feature.ndibrowser.domain.repository.TopLevelNavigationRepository
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

data class TopLevelNavUiState(
    val selectedDestination: TopLevelDestination = TopLevelDestination.HOME,
    val navLayoutProfile: NavigationLayoutProfile = NavigationLayoutProfile.PHONE_BOTTOM_NAV,
)

sealed interface TopLevelNavEvent {
    object NavigateToHome : TopLevelNavEvent
    object NavigateToStream : TopLevelNavEvent
    object NavigateToView : TopLevelNavEvent
    data class NavigationFailure(val reasonCode: String) : TopLevelNavEvent
}

/**
 * Manages top-level destination selection state with deterministic routing.
 * Re-selecting the active destination emits a no-op telemetry event without navigating.
 */
class TopLevelNavViewModel(
    private val navigationRepository: TopLevelNavigationRepository,
    private val coordinator: TopLevelNavigationCoordinator = TopLevelNavigationCoordinator(),
    private val telemetryEmitter: NavigationTelemetryEmitter = NavigationTelemetryEmitter {},
) : ViewModel() {

    private val _uiState = MutableStateFlow(TopLevelNavUiState())
    val uiState: StateFlow<TopLevelNavUiState> = _uiState.asStateFlow()

    private val _events = MutableSharedFlow<TopLevelNavEvent>(extraBufferCapacity = 1)
    val events: SharedFlow<TopLevelNavEvent> = _events.asSharedFlow()

    fun onAppLaunch(launchContext: LaunchContext) {
        viewModelScope.launch {
            val lastSaved = navigationRepository.getLastTopLevelDestination()
            val initial = coordinator.resolveInitialDestination(launchContext, lastSaved)
            _uiState.update { it.copy(selectedDestination = initial) }
            navigationRepository.saveLastTopLevelDestination(initial)
            emitNavigationEvent(initial)
        }
    }

    fun onDestinationSelected(destination: TopLevelDestination, trigger: NavigationTrigger) {
        val current = _uiState.value.selectedDestination
        if (coordinator.isNoOp(current, destination)) {
            telemetryEmitter.emit(TopLevelNavigationTelemetry.destinationReselectedNoop(destination))
            return
        }

        viewModelScope.launch {
            runCatching {
                _uiState.update { it.copy(selectedDestination = destination) }
                telemetryEmitter.emit(
                    TopLevelNavigationTelemetry.destinationSelected(current, destination, trigger.name),
                )
                navigationRepository.saveLastTopLevelDestination(destination)
                emitNavigationEvent(destination)
            }.onFailure { error ->
                val reasonCode = error.message ?: "nav_error"
                telemetryEmitter.emit(TopLevelNavigationTelemetry.navigationFailed(destination, reasonCode))
                _events.emit(TopLevelNavEvent.NavigationFailure(reasonCode))
            }
        }
    }

    fun onScreenWidthChanged(widthDp: Int) {
        val profile = coordinator.resolveLayoutProfile(widthDp)
        _uiState.update { it.copy(navLayoutProfile = profile) }
    }

    private suspend fun emitNavigationEvent(destination: TopLevelDestination) {
        val event: TopLevelNavEvent = when (destination) {
            TopLevelDestination.HOME -> TopLevelNavEvent.NavigateToHome
            TopLevelDestination.STREAM -> TopLevelNavEvent.NavigateToStream
            TopLevelDestination.VIEW -> TopLevelNavEvent.NavigateToView
        }
        _events.emit(event)
    }

    class Factory(
        private val navigationRepository: TopLevelNavigationRepository,
        private val telemetryEmitter: NavigationTelemetryEmitter = NavigationTelemetryEmitter {},
    ) : ViewModelProvider.Factory {
        override fun <T : ViewModel> create(modelClass: Class<T>): T {
            require(modelClass.isAssignableFrom(TopLevelNavViewModel::class.java))
            @Suppress("UNCHECKED_CAST")
            return TopLevelNavViewModel(navigationRepository, telemetryEmitter = telemetryEmitter) as T
        }
    }
}

