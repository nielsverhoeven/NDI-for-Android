package com.ndi.feature.ndibrowser.settings

import com.ndi.core.model.SettingsLayoutMode
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
class SettingsViewModelThreeColumnTest {

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
    fun onLayoutContextChanged_wideWidth_entersThreeColumnMode() = runTest(scheduler) {
        val viewModel = SettingsViewModel(InMemorySettingsRepository())

        viewModel.onLayoutContextChanged(widthDp = 840, isLandscape = false)
        advanceUntilIdle()

        assertEquals(SettingsLayoutMode.WIDE, viewModel.uiState.value.layoutMode)
    }

    @Test
    fun onLayoutContextChanged_compactWidth_entersCompactMode() = runTest(scheduler) {
        val viewModel = SettingsViewModel(InMemorySettingsRepository())

        viewModel.onLayoutContextChanged(widthDp = 411, isLandscape = true)
        advanceUntilIdle()

        assertEquals(SettingsLayoutMode.COMPACT, viewModel.uiState.value.layoutMode)
    }
}
