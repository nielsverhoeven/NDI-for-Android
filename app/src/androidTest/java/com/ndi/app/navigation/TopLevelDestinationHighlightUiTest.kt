package com.ndi.app.navigation

import androidx.test.ext.junit.runners.AndroidJUnit4
import com.ndi.core.model.navigation.LaunchContext
import com.ndi.core.model.navigation.NavigationOutcome
import com.ndi.core.model.navigation.NavigationTransitionRecord
import com.ndi.core.model.navigation.NavigationTrigger
import com.ndi.core.model.navigation.TopLevelDestination
import com.ndi.core.model.navigation.TopLevelDestinationState
import com.ndi.feature.ndibrowser.domain.repository.TopLevelNavigationRepository
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.runner.RunWith
import java.util.UUID

/**
 * Device-side assertions for canonical top-level destination highlight behavior.
 */
@RunWith(AndroidJUnit4::class)
class TopLevelDestinationHighlightUiTest {

    @Test
    fun selectingHome_highlightsOnlyHome() {
        val viewModel = TopLevelNavViewModel(FakeTopLevelNavigationRepository())

        assertSingleSelected(viewModel, TopLevelDestination.HOME)
    }

    @Test
    fun selectingStreamOrOutputControl_keepsStreamHighlighted() {
        val viewModel = TopLevelNavViewModel(FakeTopLevelNavigationRepository())

        viewModel.onNavDestinationObserved(TopLevelDestination.STREAM)

        assertSingleSelected(viewModel, TopLevelDestination.STREAM)
    }

    @Test
    fun selectingViewOrViewer_keepsViewHighlighted() {
        val viewModel = TopLevelNavViewModel(FakeTopLevelNavigationRepository())

        viewModel.onNavDestinationObserved(TopLevelDestination.VIEW)

        assertSingleSelected(viewModel, TopLevelDestination.VIEW)
    }

    private fun assertSingleSelected(
        viewModel: TopLevelNavViewModel,
        expectedDestination: TopLevelDestination,
    ) {
        val selected = viewModel.uiState.value.destinationItems.filter { item -> item.selected }

        assertEquals(1, selected.size)
        assertEquals(expectedDestination, selected.single().destination)
        assertEquals(expectedDestination, viewModel.uiState.value.selectedDestination)
    }
}

private class FakeTopLevelNavigationRepository : TopLevelNavigationRepository {
    private val state = MutableStateFlow(
        TopLevelDestinationState(
            destination = TopLevelDestination.HOME,
            selectedAtEpochMillis = 0L,
            launchContext = LaunchContext.LAUNCHER,
        ),
    )
    private var lastSaved: TopLevelDestination = TopLevelDestination.HOME

    override fun observeTopLevelDestination(): Flow<TopLevelDestinationState> = state.asStateFlow()

    override suspend fun selectTopLevelDestination(
        destination: TopLevelDestination,
        trigger: NavigationTrigger,
    ): NavigationTransitionRecord {
        val previous = state.value.destination
        lastSaved = destination
        state.value = state.value.copy(destination = destination, selectedAtEpochMillis = System.currentTimeMillis())
        return NavigationTransitionRecord(
            transitionId = UUID.randomUUID().toString(),
            fromDestination = previous,
            toDestination = destination,
            trigger = trigger,
            outcome = NavigationOutcome.SUCCESS,
            occurredAtEpochMillis = System.currentTimeMillis(),
        )
    }

    override suspend fun getLastTopLevelDestination(): TopLevelDestination = lastSaved

    override suspend fun saveLastTopLevelDestination(destination: TopLevelDestination) {
        lastSaved = destination
        state.value = state.value.copy(destination = destination, selectedAtEpochMillis = System.currentTimeMillis())
    }
}
