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
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.launch

data class ThemeEditorUiState(
    val selectedThemeMode: NdiThemeMode = NdiThemeMode.SYSTEM,
    val selectedAccentColorId: String = ThemeAccentPalette.defaultAccentColorId,
    val hasUnsavedChanges: Boolean = false,
)

sealed interface ThemeEditorEvent {
    data object NavigateBackToSettings : ThemeEditorEvent
}

class ThemeEditorViewModel(
    private val repository: ThemeEditorRepository,
) : ViewModel() {

    private val _uiState = MutableStateFlow(ThemeEditorUiState())
    val uiState: StateFlow<ThemeEditorUiState> = _uiState.asStateFlow()
    private val _events = MutableSharedFlow<ThemeEditorEvent>(extraBufferCapacity = 1)
    val events: SharedFlow<ThemeEditorEvent> = _events.asSharedFlow()
    private val saveMutex = Mutex()
    private var persistedPreference = ThemePreference(
        themeMode = NdiThemeMode.SYSTEM,
        accentColorId = ThemeAccentPalette.defaultAccentColorId,
        updatedAtEpochMillis = 0L,
    )

    init {
        viewModelScope.launch {
            repository.observeThemePreference().collect { preference ->
                persistedPreference = preference
                if (!_uiState.value.hasUnsavedChanges) {
                    _uiState.value = _uiState.value.copy(
                        selectedThemeMode = preference.themeMode,
                        selectedAccentColorId = preference.accentColorId,
                        hasUnsavedChanges = false,
                    )
                }
            }
        }
    }

    fun onThemeModeSelected(mode: NdiThemeMode) {
        _uiState.value = _uiState.value.copy(
            selectedThemeMode = mode,
            hasUnsavedChanges = hasDraftChanged(
                mode = mode,
                accentColorId = _uiState.value.selectedAccentColorId,
            ),
        )
        viewModelScope.launch {
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
        _uiState.value = _uiState.value.copy(
            selectedAccentColorId = accentColorId,
            hasUnsavedChanges = hasDraftChanged(
                mode = _uiState.value.selectedThemeMode,
                accentColorId = accentColorId,
            ),
        )
        viewModelScope.launch {
            ThemeEditorDependencies.telemetryEmitter.emit(
                TelemetryEvent(
                    name = TelemetryEvent.THEME_ACCENT_SELECTED,
                    timestampEpochMillis = System.currentTimeMillis(),
                    attributes = mapOf("accentColorId" to accentColorId),
                ),
            )
        }
    }

    fun onApplyClicked() {
        viewModelScope.launch {
            val state = _uiState.value
            if (state.hasUnsavedChanges) {
                saveMutex.withLock {
                    repository.saveThemePreference(
                        persistedPreference.copy(
                            themeMode = state.selectedThemeMode,
                            accentColorId = state.selectedAccentColorId,
                            updatedAtEpochMillis = System.currentTimeMillis(),
                        ),
                    )
                }
                persistedPreference = persistedPreference.copy(
                    themeMode = state.selectedThemeMode,
                    accentColorId = state.selectedAccentColorId,
                    updatedAtEpochMillis = System.currentTimeMillis(),
                )
                _uiState.value = state.copy(hasUnsavedChanges = false)
            }
            _events.emit(ThemeEditorEvent.NavigateBackToSettings)
        }
    }

    private fun hasDraftChanged(mode: NdiThemeMode, accentColorId: String): Boolean {
        return mode != persistedPreference.themeMode || accentColorId != persistedPreference.accentColorId
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
