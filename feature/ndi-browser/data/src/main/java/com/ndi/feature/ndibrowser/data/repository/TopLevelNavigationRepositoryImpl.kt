package com.ndi.feature.ndibrowser.data.repository

import com.ndi.core.model.navigation.LaunchContext
import com.ndi.core.model.navigation.NavigationOutcome
import com.ndi.core.model.navigation.NavigationTransitionRecord
import com.ndi.core.model.navigation.NavigationTrigger
import com.ndi.core.model.navigation.TopLevelDestination
import com.ndi.core.model.navigation.TopLevelDestinationState
import com.ndi.feature.ndibrowser.domain.repository.TopLevelNavigationRepository
import java.util.UUID
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow

/**
 * In-memory implementation of [TopLevelNavigationRepository].
 * Persists last destination in memory; Room persistence extension can be added later.
 */
class TopLevelNavigationRepositoryImpl : TopLevelNavigationRepository {

    @Volatile
    private var lastSaved: TopLevelDestination? = null

    private val _state = MutableStateFlow(
        TopLevelDestinationState(
            destination = TopLevelDestination.HOME,
            selectedAtEpochMillis = System.currentTimeMillis(),
            launchContext = LaunchContext.LAUNCHER,
        ),
    )

    override fun observeTopLevelDestination(): Flow<TopLevelDestinationState> =
        _state.asStateFlow()

    override suspend fun selectTopLevelDestination(
        destination: TopLevelDestination,
        trigger: NavigationTrigger,
    ): NavigationTransitionRecord {
        val current = _state.value
        val outcome = if (current.destination == destination) {
            NavigationOutcome.NO_OP_ALREADY_SELECTED
        } else {
            NavigationOutcome.SUCCESS
        }

        if (outcome == NavigationOutcome.SUCCESS) {
            _state.value = current.copy(
                destination = destination,
                selectedAtEpochMillis = System.currentTimeMillis(),
            )
            lastSaved = destination
        }

        return NavigationTransitionRecord(
            transitionId = UUID.randomUUID().toString(),
            fromDestination = current.destination,
            toDestination = destination,
            trigger = trigger,
            outcome = outcome,
            occurredAtEpochMillis = System.currentTimeMillis(),
        )
    }

    override suspend fun getLastTopLevelDestination(): TopLevelDestination? = lastSaved

    override suspend fun saveLastTopLevelDestination(destination: TopLevelDestination) {
        lastSaved = destination
        _state.value = _state.value.copy(destination = destination)
    }
}

