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
import org.junit.Assert.assertNotEquals
import org.junit.Before
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class SettingsCategorySelectionTest {

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
    fun onSettingsCategorySelected_updatesDetailWithoutChangingMainNavSelection() = runTest(scheduler) {
        val viewModel = SettingsViewModel(InMemorySettingsRepository())
        val categoriesBefore = viewModel.uiState.value.settingsCategoryState.categories

        viewModel.onSettingsCategorySelected(SettingsViewModel.CATEGORY_DISCOVERY)
        advanceUntilIdle()

        val state = viewModel.uiState.value
        assertEquals(SettingsViewModel.CATEGORY_DISCOVERY, state.settingsCategoryState.selectedCategoryId)
        assertEquals(SettingsViewModel.CATEGORY_DISCOVERY, state.settingsDetailState.selectedCategoryId)
        assertEquals(categoriesBefore.map { it.id }, state.settingsCategoryState.categories.map { it.id })
    }

    @Test
    fun onSettingsCategorySelected_aboutCategory_emitsAboutDetailGroup() = runTest(scheduler) {
        val viewModel = SettingsViewModel(InMemorySettingsRepository())

        viewModel.onSettingsCategorySelected(SettingsViewModel.CATEGORY_ABOUT)
        advanceUntilIdle()

        val detail = viewModel.uiState.value.settingsDetailState
        assertEquals(null, detail.emptyStateMessage)
        assertEquals(1, detail.groups.size)
        assertEquals("about-details", detail.groups.first().id)
    }
}
