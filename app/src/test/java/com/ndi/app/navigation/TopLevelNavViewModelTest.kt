package com.ndi.app.navigation

import com.ndi.app.R
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
import org.junit.Assert.assertTrue
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

    @Test
    fun onBackPressed_fromViewerVisible_consumesAndNavigatesToViewRoot() =
        runTest(mainDispatcherRule.dispatcher.scheduler) {
            val emittedTelemetry = mutableListOf<String>()
            val repo = FakeNavigationRepository(lastSaved = TopLevelDestination.VIEW)
            val vm = TopLevelNavViewModel(
                repo,
                telemetryEmitter = NavigationTelemetryEmitter { emittedTelemetry.add(it.name) },
            )

            val consumed = vm.onBackPressed(
                currentTopLevelDestination = TopLevelDestination.VIEW,
                isViewerVisible = true,
            )
            advanceUntilIdle()

            assertEquals(true, consumed)
            assertEquals(TopLevelDestination.VIEW, vm.uiState.value.selectedDestination)
            assert(emittedTelemetry.contains(com.ndi.core.model.TelemetryEvent.VIEW_BACK_TO_ROOT))
        }

    @Test
    fun onBackPressed_fromViewRoot_consumesAndNavigatesHome() =
        runTest(mainDispatcherRule.dispatcher.scheduler) {
            val emittedTelemetry = mutableListOf<String>()
            val repo = FakeNavigationRepository(lastSaved = TopLevelDestination.VIEW)
            val vm = TopLevelNavViewModel(
                repo,
                telemetryEmitter = NavigationTelemetryEmitter { emittedTelemetry.add(it.name) },
            )

            val consumed = vm.onBackPressed(
                currentTopLevelDestination = TopLevelDestination.VIEW,
                isViewerVisible = false,
            )
            advanceUntilIdle()

            assertEquals(true, consumed)
            assertEquals(TopLevelDestination.HOME, vm.uiState.value.selectedDestination)
            assert(emittedTelemetry.contains(com.ndi.core.model.TelemetryEvent.VIEW_ROOT_BACK_TO_HOME))
        }

    @Test
    fun destinationItems_iconMapping_matchesHomeStreamViewContract() =
        runTest(mainDispatcherRule.dispatcher.scheduler) {
            val vm = TopLevelNavViewModel(FakeNavigationRepository())

            val items = vm.uiState.value.destinationItems.associateBy { it.destination }

            assertEquals(R.drawable.ic_nav_home, items[TopLevelDestination.HOME]?.iconResId)
            assertEquals(R.drawable.ic_nav_stream, items[TopLevelDestination.STREAM]?.iconResId)
            assertEquals(R.drawable.ic_nav_view, items[TopLevelDestination.VIEW]?.iconResId)
        }

    @Test
    fun destinationItems_hasExactlyOneSelectedEntry_afterDestinationSwitches() =
        runTest(mainDispatcherRule.dispatcher.scheduler) {
            val vm = TopLevelNavViewModel(FakeNavigationRepository())

            vm.onDestinationSelected(TopLevelDestination.STREAM, NavigationTrigger.BOTTOM_NAV)
            advanceUntilIdle()

            val selectedCount = vm.uiState.value.destinationItems.count { it.selected }
            assertEquals(1, selectedCount)
            assertEquals(TopLevelDestination.STREAM, vm.uiState.value.selectedDestination)
        }

    @Test
    fun settingsToNonSettingsTransitions_emitDeterministicEventsAndSelection() =
        runTest(mainDispatcherRule.dispatcher.scheduler) {
            val vm = TopLevelNavViewModel(FakeNavigationRepository())
            val events = mutableListOf<TopLevelNavEvent>()
            val job = launch { vm.events.collect { events.add(it) } }

            vm.onAppLaunch(LaunchContext.LAUNCHER)
            advanceUntilIdle()
            vm.onDestinationSelected(TopLevelDestination.SETTINGS, NavigationTrigger.BOTTOM_NAV)
            vm.onDestinationSelected(TopLevelDestination.HOME, NavigationTrigger.BOTTOM_NAV)
            vm.onDestinationSelected(TopLevelDestination.STREAM, NavigationTrigger.BOTTOM_NAV)
            vm.onDestinationSelected(TopLevelDestination.VIEW, NavigationTrigger.BOTTOM_NAV)
            advanceUntilIdle()

            assertEquals(TopLevelDestination.VIEW, vm.uiState.value.selectedDestination)
            assertTrue(events.any { it is TopLevelNavEvent.NavigateToSettings })
            assertTrue(events.any { it is TopLevelNavEvent.NavigateToHome })
            assertTrue(events.any { it is TopLevelNavEvent.NavigateToStream })
            assertTrue(events.any { it is TopLevelNavEvent.NavigateToView })

            job.cancel()
        }

    @Test
    fun rapidSwitch_settingsAndOtherTabs_keepsSingleSelectedItemInSync() =
        runTest(mainDispatcherRule.dispatcher.scheduler) {
            val vm = TopLevelNavViewModel(FakeNavigationRepository())

            vm.onAppLaunch(LaunchContext.LAUNCHER)
            advanceUntilIdle()

            vm.onDestinationSelected(TopLevelDestination.SETTINGS, NavigationTrigger.BOTTOM_NAV)
            vm.onDestinationSelected(TopLevelDestination.STREAM, NavigationTrigger.BOTTOM_NAV)
            vm.onDestinationSelected(TopLevelDestination.SETTINGS, NavigationTrigger.BOTTOM_NAV)
            vm.onDestinationSelected(TopLevelDestination.HOME, NavigationTrigger.BOTTOM_NAV)
            vm.onDestinationSelected(TopLevelDestination.SETTINGS, NavigationTrigger.BOTTOM_NAV)
            vm.onDestinationSelected(TopLevelDestination.VIEW, NavigationTrigger.BOTTOM_NAV)
            advanceUntilIdle()

            assertEquals(TopLevelDestination.VIEW, vm.uiState.value.selectedDestination)
            assertEquals(1, vm.uiState.value.destinationItems.count { it.selected })
            assertTrue(vm.uiState.value.destinationItems.single { it.selected }.destination == TopLevelDestination.VIEW)
        }

    @Test
    fun destinationItems_includesSettingsWithExpectedIcon() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val vm = TopLevelNavViewModel(FakeNavigationRepository())

        val items = vm.uiState.value.destinationItems.associateBy { it.destination }

        assertTrue(items.containsKey(TopLevelDestination.SETTINGS))
        assertEquals(R.drawable.ic_nav_settings, items[TopLevelDestination.SETTINGS]?.iconResId)
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






