package com.ndi.feature.ndibrowser.settings

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.ndi.core.model.NdiDiscoveryEndpoint
import com.ndi.core.model.NdiSettingsSnapshot
import com.ndi.feature.ndibrowser.domain.repository.NdiSettingsRepository
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

class SettingsViewModel(
    private val settingsRepository: NdiSettingsRepository,
) : ViewModel() {

    private val _uiState = MutableStateFlow(SettingsUiState())
    val uiState: StateFlow<SettingsUiState> = _uiState.asStateFlow()

    init {
        viewModelScope.launch {
            // Load initial settings once, but don't overwrite local state changes like validation errors
            val initialSettings = settingsRepository.getSettings()
            _uiState.value = _uiState.value.copy(
                discoveryServerInput = initialSettings.discoveryServerInput.orEmpty(),
                developerModeEnabled = initialSettings.developerModeEnabled,
            )
        }
    }

    fun onDiscoveryServerChanged(input: String) {
        val validationError = validateDiscoveryInput(input)
        _uiState.value = _uiState.value.copy(
            discoveryServerInput = input,
            validationError = validationError,
        )
    }

    fun onDeveloperModeToggled(enabled: Boolean) {
        _uiState.value = _uiState.value.copy(developerModeEnabled = enabled)
        SettingsDependencies.telemetryEmitter.emit(SettingsTelemetry.developerModeToggled(enabled))
    }

    fun onSaveSettings() {
        val state = _uiState.value
        val validationError = validateDiscoveryInput(state.discoveryServerInput)
        if (validationError != null) {
            _uiState.value = _uiState.value.copy(validationError = validationError)
            return
        }

        viewModelScope.launch {
            settingsRepository.saveSettings(
                NdiSettingsSnapshot(
                    discoveryServerInput = state.discoveryServerInput.takeIf { it.isNotBlank() },
                    developerModeEnabled = state.developerModeEnabled,
                    updatedAtEpochMillis = System.currentTimeMillis(),
                ),
            )
            SettingsDependencies.telemetryEmitter.emit(
                SettingsTelemetry.discoveryServerSaved(
                    hasEndpoint = state.discoveryServerInput.isNotBlank(),
                ),
            )
        }
    }

    private fun validateDiscoveryInput(input: String): String? {
        if (input.isBlank()) return null
        return if (NdiDiscoveryEndpoint.parse(input) == null) "Invalid format" else null
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