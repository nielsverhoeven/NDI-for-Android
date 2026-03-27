package com.ndi.app.theme

import androidx.appcompat.app.AppCompatDelegate
import com.ndi.core.model.NdiThemeMode
import com.ndi.feature.themeeditor.domain.repository.ThemeEditorRepository
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.launch

class AppThemeCoordinator(
    private val themeEditorRepository: ThemeEditorRepository,
    private val scope: CoroutineScope = CoroutineScope(Dispatchers.Main.immediate + SupervisorJob()),
) {
    private var observationJob: Job? = null
    internal var lastAppliedNightMode: Int? = null

    private val _activeAccentColorId = MutableStateFlow<String?>(null)
    val activeAccentColorId: StateFlow<String?> = _activeAccentColorId.asStateFlow()

    fun start() {
        if (observationJob?.isActive == true) {
            return
        }

        observationJob = scope.launch {
            val initialPreference = themeEditorRepository.getThemePreference()
            applyNightMode(initialPreference.themeMode)
            _activeAccentColorId.value = initialPreference.accentColorId

            themeEditorRepository.observeThemePreference().collect { preference ->
                applyNightMode(preference.themeMode)
                _activeAccentColorId.value = preference.accentColorId
            }
        }
    }

    fun stop() {
        observationJob?.cancel()
        observationJob = null
    }

    private fun applyNightMode(themeMode: NdiThemeMode) {
        val nightMode = when (themeMode) {
            NdiThemeMode.LIGHT -> AppCompatDelegate.MODE_NIGHT_NO
            NdiThemeMode.DARK -> AppCompatDelegate.MODE_NIGHT_YES
            NdiThemeMode.SYSTEM -> AppCompatDelegate.MODE_NIGHT_FOLLOW_SYSTEM
        }
        lastAppliedNightMode = nightMode
        AppCompatDelegate.setDefaultNightMode(nightMode)
    }
}
