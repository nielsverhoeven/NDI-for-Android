package com.ndi.feature.ndibrowser.settings

import com.ndi.core.model.SettingsMainDestination
import kotlinx.coroutines.CoroutineStart
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.launch
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
    fun onMainNavigationSelected_emitsRoutingEventAndUpdatesSelection() = runTest(scheduler) {
        val viewModel = SettingsViewModel(InMemorySettingsRepository())
        var emitted: SettingsMainDestination? = null
        val collector = launch(start = CoroutineStart.UNDISPATCHED) {
            viewModel.mainNavigationEvents.collect {
                emitted = it
            }
        }

        viewModel.onMainNavigationSelected(SettingsMainDestination.VIEW)
        advanceUntilIdle()

        assertEquals(SettingsMainDestination.VIEW, emitted)
        val selected = viewModel.uiState.value.mainNavigationItems.single { it.isSelected }
        assertEquals(SettingsMainDestination.VIEW, selected.destination)
        collector.cancel()
    }
}
