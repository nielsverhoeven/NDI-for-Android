package com.ndi.feature.ndibrowser.viewer

import com.ndi.core.model.PlaybackState
import com.ndi.core.model.ViewerSession
import com.ndi.core.model.ViewerVideoFrame
import com.ndi.feature.ndibrowser.domain.repository.NdiViewerRepository
import com.ndi.feature.ndibrowser.domain.repository.QualityPreference
import com.ndi.feature.ndibrowser.domain.repository.QualityProfile
import com.ndi.feature.ndibrowser.domain.repository.QualityProfileRepository
import com.ndi.feature.ndibrowser.domain.repository.UserSelectionRepository
import com.ndi.feature.ndibrowser.testutil.MainDispatcherRule
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.flowOf
import kotlinx.coroutines.test.advanceUntilIdle
import kotlinx.coroutines.test.runTest
import org.junit.Assert.assertEquals
import org.junit.Rule
import org.junit.Test
import java.util.UUID

@OptIn(kotlinx.coroutines.ExperimentalCoroutinesApi::class)
class ViewerQualitySettingsViewModelTest {

    @get:Rule
    val mainDispatcherRule = MainDispatcherRule()

    @Test
    fun qualitySelection_appliesProfileAndPersistsPreference() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val viewerRepository = FakeQualityViewerRepository()
        val qualityRepository = FakeQualityProfileRepository()
        val viewModel = ViewerViewModel(
            viewerRepository = viewerRepository,
            userSelectionRepository = Us3InMemorySelectionRepository(),
            telemetryEmitter = ViewerTelemetryEmitter {},
            qualityProfileRepository = qualityRepository,
        )

        viewModel.onViewerOpened("camera-5")
        advanceUntilIdle()
        viewModel.onQualityProfileSelected("high_quality")
        advanceUntilIdle()

        assertEquals("high_quality", viewModel.uiState.value.activeQualityProfileId)
        assertEquals(QualityProfile.HIGH_QUALITY, viewerRepository.lastAppliedProfile)
        assertEquals(QualityProfile.HIGH_QUALITY.id, qualityRepository.lastSaved?.profileId)
    }

    @Test
    fun viewerOpen_rehydratesStoredPreference() = runTest(mainDispatcherRule.dispatcher.scheduler) {
        val viewerRepository = FakeQualityViewerRepository()
        val qualityRepository = FakeQualityProfileRepository(
            initialPreference = QualityPreference(sourceId = "camera-5", profileId = QualityProfile.BALANCED.id),
        )
        val viewModel = ViewerViewModel(
            viewerRepository = viewerRepository,
            userSelectionRepository = Us3InMemorySelectionRepository(),
            telemetryEmitter = ViewerTelemetryEmitter {},
            qualityProfileRepository = qualityRepository,
        )

        viewModel.onViewerOpened("camera-5")
        advanceUntilIdle()

        assertEquals(QualityProfile.BALANCED.profileId, viewModel.uiState.value.activeQualityProfileId)
        assertEquals(QualityProfile.BALANCED, viewerRepository.lastAppliedProfile)
    }
}

private class FakeQualityViewerRepository : NdiViewerRepository {
    private val sessions = MutableStateFlow(
        ViewerSession(
            sessionId = UUID.randomUUID().toString(),
            selectedSourceId = "",
            playbackState = PlaybackState.IDLE,
            startedAtEpochMillis = 0L,
        ),
    )

    var lastAppliedProfile: QualityProfile = QualityProfile.default()

    override suspend fun connectToSource(sourceId: String): ViewerSession {
        val session = ViewerSession(
            sessionId = UUID.randomUUID().toString(),
            selectedSourceId = sourceId,
            playbackState = PlaybackState.PLAYING,
            startedAtEpochMillis = System.currentTimeMillis(),
        )
        sessions.value = session
        return session
    }

    override fun observeViewerSession(): Flow<ViewerSession> = sessions

    override fun getLatestVideoFrame(): ViewerVideoFrame? = null

    override suspend fun retryReconnectWithinWindow(sourceId: String, windowSeconds: Int): ViewerSession {
        return connectToSource(sourceId)
    }

    override suspend fun stopViewing() {
        sessions.value = sessions.value.copy(playbackState = PlaybackState.STOPPED)
    }

    override suspend fun applyQualityProfile(sourceId: String, profile: QualityProfile) {
        lastAppliedProfile = profile
    }
}

private class FakeQualityProfileRepository(
    private val initialPreference: QualityPreference = QualityPreference(profileId = QualityProfile.default().id),
) : QualityProfileRepository {
    var lastSaved: QualityPreference? = null

    override suspend fun getAllProfiles(): List<QualityProfile> {
        return QualityProfile.all()
    }

    override fun observeQualityPreference(sourceId: String?): Flow<QualityPreference> {
        return flowOf(lastSaved ?: initialPreference.copy(sourceId = sourceId))
    }

    override suspend fun getQualityPreference(sourceId: String?): QualityPreference {
        return (lastSaved ?: initialPreference).copy(sourceId = sourceId)
    }

    override suspend fun setQualityPreference(preference: QualityPreference) {
        lastSaved = preference
    }

    override suspend fun clearPreferences() {
        lastSaved = null
    }
}

private class Us3InMemorySelectionRepository : UserSelectionRepository {
    private var sourceId: String? = null

    override suspend fun saveLastSelectedSource(sourceId: String) {
        this.sourceId = sourceId
    }

    override suspend fun getLastSelectedSource(): String? = sourceId
}
