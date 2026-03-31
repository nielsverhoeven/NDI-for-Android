package com.ndi.feature.ndibrowser.settings

import com.ndi.core.model.NdiSettingsSnapshot
import com.ndi.core.model.SettingsLayoutMode
import com.ndi.feature.ndibrowser.domain.repository.NdiSettingsRepository
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class SettingsFragmentWideLayoutTest {

    private val dispatcher = StandardTestDispatcher()

    @Before
    fun setUp() {
        Dispatchers.setMain(dispatcher)
    }

    @After
    fun tearDown() {
        Dispatchers.resetMain()
    }

    @Test
    fun wideLayout_appearanceSelection_keepsInlineAppearanceControlContract() = runTest {
        val viewModel = SettingsViewModel(WideTestSettingsRepository())
        viewModel.onLayoutContextChanged(widthDp = 900, isLandscape = true)
        viewModel.onSettingsCategorySelected(SettingsViewModel.CATEGORY_APPEARANCE)

        val state = viewModel.uiState.value
        assertEquals(SettingsLayoutMode.WIDE, state.layoutMode)
        assertTrue(state.settingsDetailState.groups.first().controls.contains("theme-mode"))
        assertTrue(state.settingsDetailState.groups.first().controls.contains("accent-palette"))
    }
}

private class WideTestSettingsRepository : NdiSettingsRepository {
    private val flow = MutableStateFlow(
        NdiSettingsSnapshot(
            discoveryServerInput = null,
            developerModeEnabled = false,
            themeMode = com.ndi.core.model.NdiThemeMode.SYSTEM,
            accentColorId = "accent_teal",
            updatedAtEpochMillis = 0L,
        ),
    )

    override suspend fun getSettings(): NdiSettingsSnapshot = flow.value

    override suspend fun saveSettings(snapshot: NdiSettingsSnapshot) {
        flow.value = snapshot
    }

    override fun observeSettings(): Flow<NdiSettingsSnapshot> = flow
}
