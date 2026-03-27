package com.ndi.feature.themeeditor

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.ndi.core.model.NdiThemeMode
import com.ndi.core.model.TelemetryEvent
import com.ndi.feature.themeeditor.domain.model.ThemeAccentPalette
import com.ndi.feature.themeeditor.domain.model.ThemePreference
import com.ndi.feature.themeeditor.domain.repository.ThemeEditorRepository
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

data class ThemeEditorUiState(
    val selectedThemeMode: NdiThemeMode = NdiThemeMode.SYSTEM,
    val selectedAccentColorId: String = ThemeAccentPalette.defaultAccentColorId,
)

class ThemeEditorViewModel(
    private val repository: ThemeEditorRepository,
) : ViewModel() {

    private val _uiState = MutableStateFlow(ThemeEditorUiState())
    val uiState: StateFlow<ThemeEditorUiState> = _uiState.asStateFlow()
    private val saveMutex = Mutex()

    init {
        viewModelScope.launch {
            repository.observeThemePreference().collect { preference ->
                _uiState.value = _uiState.value.copy(
                    selectedThemeMode = preference.themeMode,
                    selectedAccentColorId = preference.accentColorId,
                )
            }
        }
    }

    fun onThemeModeSelected(mode: NdiThemeMode) {
        viewModelScope.launch {
            saveMutex.withLock {
                val current = repository.getThemePreference()
                repository.saveThemePreference(
                    current.copy(
                        themeMode = mode,
                        updatedAtEpochMillis = System.currentTimeMillis(),
                    ),
                )
            }
            ThemeEditorDependencies.telemetryEmitter.emit(
                TelemetryEvent(
                    name = TelemetryEvent.THEME_MODE_SELECTED,
                    timestampEpochMillis = System.currentTimeMillis(),
                    attributes = mapOf("themeMode" to mode.name),
                ),
            )
        }
    }

    fun onAccentColorSelected(accentColorId: String) {
        viewModelScope.launch {
            saveMutex.withLock {
                val current = repository.getThemePreference()
                repository.saveThemePreference(
                    current.copy(
                        accentColorId = accentColorId,
                        updatedAtEpochMillis = System.currentTimeMillis(),
                    ),
                )
            }
            ThemeEditorDependencies.telemetryEmitter.emit(
                TelemetryEvent(
                    name = TelemetryEvent.THEME_ACCENT_SELECTED,
                    timestampEpochMillis = System.currentTimeMillis(),
                    attributes = mapOf("accentColorId" to accentColorId),
                ),
            )
        }
    }

    class Factory(
        private val repository: ThemeEditorRepository,
    ) : ViewModelProvider.Factory {
        @Suppress("UNCHECKED_CAST")
        override fun <T : ViewModel> create(modelClass: Class<T>): T {
            return ThemeEditorViewModel(repository) as T
        }
    }
}
