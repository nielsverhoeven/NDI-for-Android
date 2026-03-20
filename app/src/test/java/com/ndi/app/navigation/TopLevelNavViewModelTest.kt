package com.ndi.app.navigation

import com.ndi.app.testutil.MainDispatcherRule
import com.ndi.core.model.navigation.LaunchContext
import com.ndi.core.model.navigation.NavigationOutcome
import com.ndi.core.model.navigation.NavigationTransitionRecord
import com.ndi.core.model.navigation.NavigationTrigger
import com.ndi.core.model.navigation.TopLevelDestination
import com.ndi.core.model.navigation.TopLevelDestinationState
import com.ndi.feature.ndibrowser.domain.repository.TopLevelNavigationRepository
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.launch
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class TopLevelNavViewModelTest {

    @get:Rule
    val mainDispatcherRule = MainDispatcherRule()

    @Test
    fun launcherContext_resolvesToHome() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val repo = FakeNavigationRepository()
        val vm = TopLevelNavViewModel(repo)
        vm.onAppLaunch(LaunchContext.LAUNCHER)
        advanceUntilIdle()
        assertEquals(TopLevelDestination.HOME, vm.uiState.value.selectedDestination)
    }

    @Test
    fun recentsRestore_withSavedDestination_restoresThatDestination() =
        runTest(mainDispatcherRule.dispatcher.scheduler) {
            val repo = FakeNavigationRepository(lastSaved = TopLevelDestination.STREAM)
            val vm = TopLevelNavViewModel(repo)
            vm.onAppLaunch(LaunchContext.RECENTS_RESTORE)
            advanceUntilIdle()
            assertEquals(TopLevelDestination.STREAM, vm.uiState.value.selectedDestination)
        }

    @Test
    fun onDestinationSelected_updatesSelectedDestination() =
        runTest(mainDispatcherRule.dispatcher.scheduler) {
            val repo = FakeNavigationRepository()
            val vm = TopLevelNavViewModel(repo)
            vm.onAppLaunch(LaunchContext.LAUNCHER)
            advanceUntilIdle()

            vm.onDestinationSelected(TopLevelDestination.STREAM, NavigationTrigger.BOTTOM_NAV)
            advanceUntilIdle()

            assertEquals(TopLevelDestination.STREAM, vm.uiState.value.selectedDestination)
        }

    @Test
    fun reselecting_currentDestination_isNoOp() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val emitted = mutableListOf<String>()
        val repo = FakeNavigationRepository()
        val vm = TopLevelNavViewModel(repo, telemetryEmitter = NavigationTelemetryEmitter { emitted.add(it.name) })

        vm.onAppLaunch(LaunchContext.LAUNCHER)
        advanceUntilIdle()

        // Select HOME again (already HOME)
        vm.onDestinationSelected(TopLevelDestination.HOME, NavigationTrigger.BOTTOM_NAV)
        advanceUntilIdle()

        // The current dest must not have changed
        assertEquals(TopLevelDestination.HOME, vm.uiState.value.selectedDestination)
        // No-op telemetry event emitted
        assert(emitted.any { it.contains("reselected") || it.contains("noop") })
    }

    @Test
    fun navigatingToAllDestinations_emitsCorrectEvents() =
        runTest(mainDispatcherRule.dispatcher.scheduler) {
            val repo = FakeNavigationRepository()
            val vm = TopLevelNavViewModel(repo)
            val events = mutableListOf<TopLevelNavEvent>()

            val job = launch { vm.events.collect { events.add(it) } }

            vm.onAppLaunch(LaunchContext.LAUNCHER)
            advanceUntilIdle()
            vm.onDestinationSelected(TopLevelDestination.STREAM, NavigationTrigger.BOTTOM_NAV)
            advanceUntilIdle()
            vm.onDestinationSelected(TopLevelDestination.VIEW, NavigationTrigger.BOTTOM_NAV)
            advanceUntilIdle()

            assert(events.any { it is TopLevelNavEvent.NavigateToHome })
            assert(events.any { it is TopLevelNavEvent.NavigateToStream })
            assert(events.any { it is TopLevelNavEvent.NavigateToView })

            job.cancel()
        }
}

private class FakeNavigationRepository(
    private val lastSaved: TopLevelDestination? = null,
) : TopLevelNavigationRepository {
    private val _state = MutableStateFlow(
        TopLevelDestinationState(
            destination = TopLevelDestination.HOME,
            selectedAtEpochMillis = 0L,
            launchContext = LaunchContext.LAUNCHER,
        ),
    )
    private var saved = lastSaved

    override fun observeTopLevelDestination(): Flow<TopLevelDestinationState> = _state

    override suspend fun selectTopLevelDestination(
        destination: TopLevelDestination,
        trigger: NavigationTrigger,
    ): NavigationTransitionRecord {
        val prev = _state.value.destination
        _state.value = _state.value.copy(destination = destination)
        saved = destination
        return NavigationTransitionRecord(
            transitionId = "test",
            fromDestination = prev,
            toDestination = destination,
            trigger = trigger,
            outcome = NavigationOutcome.SUCCESS,
            occurredAtEpochMillis = 0L,
        )
    }

    override suspend fun getLastTopLevelDestination(): TopLevelDestination? = saved

    override suspend fun saveLastTopLevelDestination(destination: TopLevelDestination) {
        saved = destination
    }
}






