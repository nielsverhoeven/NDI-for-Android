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
import kotlinx.coroutines.flow.launchIn
import kotlinx.coroutines.flow.onEach
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

        const val ACCENT_BLUE = "accent_blue"
        const val ACCENT_TEAL = "accent_teal"
        const val ACCENT_GREEN = "accent_green"
        const val ACCENT_ORANGE = "accent_orange"
        const val ACCENT_RED = "accent_red"
        const val ACCENT_PINK = "accent_pink"
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
                accentColorId = normalizeAccent(initialSettings.accentColorId),
                settingsDetailState = buildDetailState(lastSelectedCategoryId),
            )
        }

        SettingsDependencies.overlayStateFlowOrNull()
            ?.onEach { overlay ->
                val enabled = _uiState.value.developerModeEnabled
                _uiState.value = _uiState.value.copy(
                    overlayDisplayState = if (enabled) overlay else null,
                )
            }
            ?.launchIn(viewModelScope)
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
            isDirty = computeIsDirty(
                themeMode = mode,
                accentColorId = _uiState.value.accentColorId,
                developerModeEnabled = _uiState.value.developerModeEnabled,
            ),
            savedConfirmationVisible = false,
        )
    }

    fun onAccentColorChanged(accentColorId: String) {
        val normalizedAccent = normalizeAccent(accentColorId)
        _uiState.value = _uiState.value.copy(
            accentColorId = normalizedAccent,
            isDirty = computeIsDirty(
                themeMode = _uiState.value.themeMode,
                accentColorId = normalizedAccent,
                developerModeEnabled = _uiState.value.developerModeEnabled,
            ),
            savedConfirmationVisible = false,
        )
        SettingsDependencies.telemetryEmitter.emit(SettingsTelemetry.themeAccentSelected(normalizedAccent))
    }

    fun onDeveloperModeToggled(enabled: Boolean) {
        _uiState.value = _uiState.value.copy(
            developerModeEnabled = enabled,
            isDirty = computeIsDirty(
                themeMode = _uiState.value.themeMode,
                accentColorId = _uiState.value.accentColorId,
                developerModeEnabled = enabled,
            ),
            savedConfirmationVisible = false,
            overlayDisplayState = if (enabled) _uiState.value.overlayDisplayState else null,
        )
        SettingsDependencies.telemetryEmitter.emit(SettingsTelemetry.developerModeToggled(enabled))
    }

    fun onSaveSettings() {
        val state = _uiState.value

        viewModelScope.launch {
            settingsRepository.saveSettings(
                NdiSettingsSnapshot(
                    // Discovery endpoint is now controlled exclusively by Discovery Servers submenu.
                    discoveryServerInput = null,
                    developerModeEnabled = state.developerModeEnabled,
                    themeMode = state.themeMode,
                    accentColorId = normalizeAccent(state.accentColorId),
                    updatedAtEpochMillis = System.currentTimeMillis(),
                ),
            )
            baselineSettings = baselineSettings?.copy(
                developerModeEnabled = state.developerModeEnabled,
                themeMode = state.themeMode,
                accentColorId = normalizeAccent(state.accentColorId),
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
                groups = listOf(SettingsDetailGroup("appearance-controls", "Appearance Settings", listOf("theme-mode", "accent-palette"))),
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
                groups = listOf(SettingsDetailGroup("about-details", "About", listOf("app-version"))),
                emptyStateMessage = null,
                isEditable = false,
            )
        }
    }

    private fun defaultUiState(): SettingsUiState {
        val selectedCategory = CATEGORY_GENERAL
        return SettingsUiState(
            themeMode = NdiThemeMode.SYSTEM,
            accentColorId = ACCENT_TEAL,
            isDirty = false,
            savedConfirmationVisible = false,
            layoutMode = SettingsLayoutMode.COMPACT,
            settingsCategoryState = buildCategoryState(selectedCategory, SettingsCategorySelectionSource.DEFAULT),
            settingsDetailState = buildDetailState(selectedCategory),
        )
    }

    private fun computeIsDirty(themeMode: NdiThemeMode, accentColorId: String, developerModeEnabled: Boolean): Boolean {
        val baseline = baselineSettings ?: return false
        return themeMode != baseline.themeMode ||
            normalizeAccent(accentColorId) != normalizeAccent(baseline.accentColorId) ||
            developerModeEnabled != baseline.developerModeEnabled
    }

    private fun normalizeAccent(accentColorId: String): String {
        return when (accentColorId) {
            ACCENT_BLUE,
            ACCENT_TEAL,
            ACCENT_GREEN,
            ACCENT_ORANGE,
            ACCENT_RED,
            ACCENT_PINK,
            -> accentColorId
            else -> ACCENT_TEAL
        }
    }
}