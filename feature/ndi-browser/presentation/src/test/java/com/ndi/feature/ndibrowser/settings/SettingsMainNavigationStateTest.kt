package com.ndi.feature.ndibrowser.settings

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.test.StandardTestDispatcher
import kotlinx.coroutines.test.TestCoroutineScheduler
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.resetMain
import kotlinx.coroutines.test.runTest
import kotlinx.coroutines.test.setMain
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Before
import org.junit.Test
import java.io.File

@OptIn(ExperimentalCoroutinesApi::class)
class SettingsMainNavigationStateTest {

    private val scheduler = TestCoroutineScheduler()
    private val dispatcher = StandardTestDispatcher(scheduler)

    @Before
    fun setUp() {
        Dispatchers.setMain(dispatcher)
    }

    @After
    fun tearDown() {
        Dispatchers.resetMain()
    }

    @Test
    fun onSettingsCategorySelected_marksOnlyOneCategorySelected() = runTest(scheduler) {
        val viewModel = SettingsViewModel(InMemorySettingsRepository())

        viewModel.onSettingsCategorySelected(SettingsViewModel.CATEGORY_DISCOVERY)
        advanceUntilIdle()

        val selected = viewModel.uiState.value.settingsCategoryState.categories.single { it.isSelected }
        assertEquals(SettingsViewModel.CATEGORY_DISCOVERY, selected.id)
        assertEquals(SettingsViewModel.CATEGORY_DISCOVERY, viewModel.uiState.value.settingsDetailState.selectedCategoryId)
    }

    @Test
    fun consistencyContract_settingsAndDiscoveryButtonsUseCanonicalStyles() {
        val settingsLayout = File("src/main/res/layout/fragment_settings.xml").readText()
        val navPanelLayout = File("src/main/res/layout/view_settings_main_navigation_panel.xml").readText()
        val discoveryLayout = File("src/main/res/layout/fragment_discovery_server_settings.xml").readText()
        val discoveryLayoutWide = File("src/main/res/layout-sw600dp/fragment_discovery_server_settings.xml").readText()
        val discoveryRow = File("src/main/res/layout/item_discovery_server.xml").readText()

        assertTrue(settingsLayout.contains("style=\"@style/Widget.NdiBrowser.Button\""))
        assertTrue(navPanelLayout.contains("style=\"@style/Widget.NdiBrowser.Button.Outlined\""))
        assertTrue(discoveryLayout.contains("style=\"@style/Widget.NdiBrowser.Button\""))
        assertTrue(discoveryLayoutWide.contains("style=\"@style/Widget.NdiBrowser.Button\""))
        assertTrue(discoveryRow.contains("style=\"@style/Widget.NdiBrowser.Button.Icon\""))

        assertFalse(settingsLayout.contains("Widget.Material3.Button"))
        assertFalse(navPanelLayout.contains("Widget.Material3.Button"))
        assertFalse(discoveryLayout.contains("Widget.Material3.Button"))
        assertFalse(discoveryLayoutWide.contains("Widget.Material3.Button"))
        assertFalse(discoveryRow.contains("Widget.Material3.Button"))
    }
}
