package com.ndi.feature.ndibrowser.settings

import androidx.lifecycle.ViewModel
import androidx.lifecycle.ViewModelProvider
import androidx.lifecycle.viewModelScope
import com.ndi.core.model.NdiSettingsSnapshot
import com.ndi.core.model.NdiThemeMode
import com.ndi.core.model.SettingsCategory
import com.ndi.core.model.SettingsCategorySelectionSource
import com.ndi.core.model.SettingsCategoryState
import com.ndi.core.model.SettingsDetailGroup
import com.ndi.core.model.SettingsDetailState
import com.ndi.core.model.SettingsLayoutMode
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
    private val layoutResolver: SettingsLayoutResolver = SettingsLayoutResolver,
) : ViewModel() {

    companion object {
        const val CATEGORY_GENERAL = "general"
        const val CATEGORY_APPEARANCE = "appearance"
        const val CATEGORY_DISCOVERY = "discovery"
        const val CATEGORY_DEVELOPER = "developer"
        const val CATEGORY_ABOUT = "about"
    }

    private val _uiState = MutableStateFlow(defaultUiState())
    val uiState: StateFlow<SettingsUiState> = _uiState.asStateFlow()

    private val _closeSettingsEvents = MutableSharedFlow<Unit>(extraBufferCapacity = 1)
    val closeSettingsEvents: SharedFlow<Unit> = _closeSettingsEvents.asSharedFlow()

    private var closeSettingsInFlight: Boolean = false
    private var lastSelectedCategoryId: String = CATEGORY_GENERAL
    private var baselineSettings: NdiSettingsSnapshot? = null

    init {
        viewModelScope.launch {
            val initialSettings = settingsRepository.getSettings()
            baselineSettings = initialSettings
            _uiState.value = _uiState.value.copy(
                developerModeEnabled = initialSettings.developerModeEnabled,
                themeMode = initialSettings.themeMode,
                settingsDetailState = buildDetailState(lastSelectedCategoryId),
            )
        }
    }

    fun onLayoutContextChanged(widthDp: Int, isLandscape: Boolean) {
        val mode = layoutResolver.resolve(widthDp, isLandscape)
        val selectedCategoryId = _uiState.value.settingsCategoryState.selectedCategoryId ?: lastSelectedCategoryId
        _uiState.value = _uiState.value.copy(
            layoutMode = mode,
            settingsCategoryState = buildCategoryState(
                selectedCategoryId = selectedCategoryId,
                source = SettingsCategorySelectionSource.RESTORED,
            ),
            settingsDetailState = buildDetailState(selectedCategoryId),
        )
    }

    fun onSettingsCategorySelected(categoryId: String) {
        lastSelectedCategoryId = categoryId
        _uiState.value = _uiState.value.copy(
            settingsCategoryState = buildCategoryState(
                selectedCategoryId = categoryId,
                source = SettingsCategorySelectionSource.USER_TAP,
            ),
            settingsDetailState = buildDetailState(categoryId),
        )
    }

    fun onThemeModeChanged(mode: NdiThemeMode) {
        _uiState.value = _uiState.value.copy(
            themeMode = mode,
            isDirty = computeIsDirty(themeMode = mode, developerModeEnabled = _uiState.value.developerModeEnabled),
            savedConfirmationVisible = false,
        )
    }

    fun onDeveloperModeToggled(enabled: Boolean) {
        _uiState.value = _uiState.value.copy(
            developerModeEnabled = enabled,
            isDirty = computeIsDirty(themeMode = _uiState.value.themeMode, developerModeEnabled = enabled),
            savedConfirmationVisible = false,
        )
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
                    themeMode = state.themeMode,
                    accentColorId = currentSettings.accentColorId,
                    updatedAtEpochMillis = System.currentTimeMillis(),
                ),
            )
            baselineSettings = baselineSettings?.copy(
                developerModeEnabled = state.developerModeEnabled,
                themeMode = state.themeMode,
            )
            _uiState.value = _uiState.value.copy(
                isDirty = false,
                savedConfirmationVisible = true,
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
        private val layoutResolver: SettingsLayoutResolver,
    ) : ViewModelProvider.Factory {
        @Suppress("UNCHECKED_CAST")
        override fun <T : ViewModel> create(modelClass: Class<T>): T {
            return SettingsViewModel(settingsRepository, layoutResolver) as T
        }
    }

    private fun buildCategoryState(
        selectedCategoryId: String,
        source: SettingsCategorySelectionSource,
    ): SettingsCategoryState {
        return SettingsCategoryState(
            categories = listOf(
                SettingsCategory(CATEGORY_GENERAL, "General", "Save and apply current settings", selectedCategoryId == CATEGORY_GENERAL, true),
                SettingsCategory(CATEGORY_APPEARANCE, "Appearance", "Theme mode and accent", selectedCategoryId == CATEGORY_APPEARANCE, true),
                SettingsCategory(CATEGORY_DISCOVERY, "Discovery", "NDI discovery server endpoints", selectedCategoryId == CATEGORY_DISCOVERY, true),
                SettingsCategory(CATEGORY_DEVELOPER, "Developer tools", "Overlay and diagnostics toggles", selectedCategoryId == CATEGORY_DEVELOPER, true),
                SettingsCategory(CATEGORY_ABOUT, "About", "App and feature information", selectedCategoryId == CATEGORY_ABOUT, false),
            ),
            selectedCategoryId = selectedCategoryId,
            selectionSource = source,
        )
    }

    private fun buildDetailState(selectedCategoryId: String): SettingsDetailState {
        return when (selectedCategoryId) {
            CATEGORY_GENERAL -> SettingsDetailState(
                selectedCategoryId = selectedCategoryId,
                groups = listOf(SettingsDetailGroup("general-controls", "General Settings", listOf("save"))),
                emptyStateMessage = null,
                isEditable = true,
            )
            CATEGORY_APPEARANCE -> SettingsDetailState(
                selectedCategoryId = selectedCategoryId,
                groups = listOf(SettingsDetailGroup("appearance-controls", "Appearance Settings", listOf("theme-editor"))),
                emptyStateMessage = null,
                isEditable = true,
            )
            CATEGORY_DISCOVERY -> SettingsDetailState(
                selectedCategoryId = selectedCategoryId,
                groups = listOf(SettingsDetailGroup("discovery-controls", "Discovery Settings", listOf("discovery-servers"))),
                emptyStateMessage = null,
                isEditable = true,
            )
            CATEGORY_DEVELOPER -> SettingsDetailState(
                selectedCategoryId = selectedCategoryId,
                groups = listOf(SettingsDetailGroup("developer-controls", "Developer Settings", listOf("developer-mode-toggle"))),
                emptyStateMessage = null,
                isEditable = true,
            )
            else -> SettingsDetailState(
                selectedCategoryId = selectedCategoryId,
                groups = emptyList(),
                emptyStateMessage = "No direct controls are available in About.",
                isEditable = false,
            )
        }
    }

    private fun defaultUiState(): SettingsUiState {
        val selectedCategory = CATEGORY_GENERAL
        return SettingsUiState(
            themeMode = NdiThemeMode.SYSTEM,
            isDirty = false,
            savedConfirmationVisible = false,
            layoutMode = SettingsLayoutMode.COMPACT,
            settingsCategoryState = buildCategoryState(selectedCategory, SettingsCategorySelectionSource.DEFAULT),
            settingsDetailState = buildDetailState(selectedCategory),
        )
    }

    private fun computeIsDirty(themeMode: NdiThemeMode, developerModeEnabled: Boolean): Boolean {
        val baseline = baselineSettings ?: return false
        return themeMode != baseline.themeMode || developerModeEnabled != baseline.developerModeEnabled
    }
}