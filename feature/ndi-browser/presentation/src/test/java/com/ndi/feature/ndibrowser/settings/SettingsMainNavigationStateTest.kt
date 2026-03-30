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
import org.junit.Before
import org.junit.Test

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
}
