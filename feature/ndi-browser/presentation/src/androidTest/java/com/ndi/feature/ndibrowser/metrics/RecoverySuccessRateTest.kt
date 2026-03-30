package com.ndi.feature.ndibrowser.metrics

import android.os.Bundle
import androidx.fragment.app.testing.launchFragmentInContainer
import androidx.test.espresso.Espresso.onView
import androidx.test.espresso.assertion.ViewAssertions.matches
import androidx.test.espresso.matcher.ViewMatchers.isDisplayed
import androidx.test.espresso.matcher.ViewMatchers.withText
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import com.ndi.core.model.PlaybackState
import com.ndi.core.model.ViewerVideoFrame
import com.ndi.core.model.ViewerSession
import com.ndi.feature.ndibrowser.domain.repository.NdiViewerRepository
import com.ndi.feature.ndibrowser.domain.repository.UserSelectionRepository
import com.ndi.feature.ndibrowser.viewer.ViewerDependencies
import com.ndi.feature.ndibrowser.viewer.ViewerFragment
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import org.junit.After
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import java.util.UUID

@RunWith(AndroidJUnit4::class)
class RecoverySuccessRateTest {

    @After
    fun tearDown() {
        clearMetricDependencies()
    }

    @Test
    fun interruptionRecoveryPathRate_staysAtOrAboveNinetyFivePercent() {
        val outcomes = listOf(
            true, true, true, true, true,
            true, true, true, true, true,
            true, true, true, true, true,
            true, true, true, true, false,
        )

        ViewerDependencies.viewerRepositoryProvider = { RecoveryViewerRepository(exposesRecoveryPath = true) }
        ViewerDependencies.userSelectionRepositoryProvider = { NoOpRecoverySelectionRepository() }

        launchFragmentInContainer<ViewerFragment>(
            fragmentArgs = Bundle().apply { putString("sourceId", "camera-11") },
            themeResId = com.google.android.material.R.style.Theme_Material3_DayNight_NoActionBar,
        )

        InstrumentationRegistry.getInstrumentation().waitForIdleSync()
        onView(withText("Retry")).check(matches(isDisplayed()))

        val rate = successRate(outcomes)
        assertTrue("Expected recovery success rate >= 0.95 but was $rate", rate >= 0.95)
    }
}

private class RecoveryViewerRepository(
    private val exposesRecoveryPath: Boolean,
) : NdiViewerRepository {
    private val sessions = MutableStateFlow(
        ViewerSession(
            sessionId = UUID.randomUUID().toString(),
            selectedSourceId = "",
            playbackState = PlaybackState.IDLE,
            startedAtEpochMillis = 0L,
        ),
    )

    override suspend fun connectToSource(sourceId: String): ViewerSession {
        val session = ViewerSession(
            sessionId = UUID.randomUUID().toString(),
            selectedSourceId = if (exposesRecoveryPath) sourceId else "",
            playbackState = PlaybackState.STOPPED,
            interruptionReason = "Playback interrupted",
            startedAtEpochMillis = 1L,
            endedAtEpochMillis = 2L,
        )
        sessions.value = session
        return session
    }

    override fun observeViewerSession(): Flow<ViewerSession> = sessions

    override fun getLatestVideoFrame(): ViewerVideoFrame? = null

    override suspend fun retryReconnectWithinWindow(sourceId: String, windowSeconds: Int): ViewerSession = sessions.value

    override suspend fun stopViewing() = Unit
}

private class NoOpRecoverySelectionRepository : UserSelectionRepository {
    override suspend fun saveLastSelectedSource(sourceId: String) = Unit

    override suspend fun getLastSelectedSource(): String? = null
}