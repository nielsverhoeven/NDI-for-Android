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
class SettingsDetailStateFallbackTest {

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
    fun aboutCategory_detailStateRetainsGroupAcrossTransitions() = runTest(scheduler) {
        val viewModel = SettingsViewModel(InMemorySettingsRepository())

        viewModel.onSettingsCategorySelected(SettingsViewModel.CATEGORY_ABOUT)
        viewModel.onLayoutContextChanged(widthDp = 411, isLandscape = false)
        viewModel.onLayoutContextChanged(widthDp = 720, isLandscape = true)
        advanceUntilIdle()

        val detail = viewModel.uiState.value.settingsDetailState
        assertEquals(SettingsViewModel.CATEGORY_ABOUT, detail.selectedCategoryId)
        assertEquals(null, detail.emptyStateMessage)
        assertEquals(1, detail.groups.size)
        assertEquals("about-details", detail.groups.first().id)
    }
}
