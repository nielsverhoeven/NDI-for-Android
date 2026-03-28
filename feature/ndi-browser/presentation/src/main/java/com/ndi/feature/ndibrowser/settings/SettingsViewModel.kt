package com.ndi.feature.ndibrowser.settings

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.ndi.core.model.NdiSettingsSnapshot
import com.ndi.feature.ndibrowser.domain.repository.NdiSettingsRepository
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

class SettingsViewModel(
    private val settingsRepository: NdiSettingsRepository,
) : ViewModel() {

    private val _uiState = MutableStateFlow(SettingsUiState())
    val uiState: StateFlow<SettingsUiState> = _uiState.asStateFlow()

    private val _closeSettingsEvents = MutableSharedFlow<Unit>(extraBufferCapacity = 1)
    val closeSettingsEvents: SharedFlow<Unit> = _closeSettingsEvents.asSharedFlow()
    private var closeSettingsInFlight: Boolean = false

    init {
        viewModelScope.launch {
            // Load initial settings once, but don't overwrite local state changes like validation errors
            val initialSettings = settingsRepository.getSettings()
            _uiState.value = _uiState.value.copy(
                developerModeEnabled = initialSettings.developerModeEnabled,
            )
        }
    }

    fun onDeveloperModeToggled(enabled: Boolean) {
        _uiState.value = _uiState.value.copy(developerModeEnabled = enabled)
        SettingsDependencies.telemetryEmitter.emit(SettingsTelemetry.developerModeToggled(enabled))
    }

    fun onSaveSettings() {
        val state = _uiState.value

        viewModelScope.launch {
            val currentSettings = settingsRepository.getSettings()
            settingsRepository.saveSettings(
                NdiSettingsSnapshot(
                    // Discovery endpoint is now controlled exclusively by Discovery Servers submenu.
                    discoveryServerInput = currentSettings.discoveryServerInput,
                    developerModeEnabled = state.developerModeEnabled,
                    themeMode = currentSettings.themeMode,
                    accentColorId = currentSettings.accentColorId,
                    updatedAtEpochMillis = System.currentTimeMillis(),
                ),
            )
        }
    }

    fun onSettingsTogglePressed() {
        if (closeSettingsInFlight) return
        closeSettingsInFlight = true
        _closeSettingsEvents.tryEmit(Unit)
    }

    fun onCloseSettingsSettled() {
        closeSettingsInFlight = false
    }

    class Factory(
        private val settingsRepository: NdiSettingsRepository,
    ) : ViewModelProvider.Factory {
        @Suppress("UNCHECKED_CAST")
        override fun <T : ViewModel> create(modelClass: Class<T>): T {
            return SettingsViewModel(settingsRepository) as T
        }
    }
}