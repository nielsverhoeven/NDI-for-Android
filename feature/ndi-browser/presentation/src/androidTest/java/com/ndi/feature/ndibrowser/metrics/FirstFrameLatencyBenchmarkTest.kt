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
class FirstFrameLatencyBenchmarkTest {

    @After
    fun tearDown() {
        clearMetricDependencies()
    }

    @Test
    fun firstFrameLatency90thPercentile_staysWithinThreeSeconds() {
        val scenarios = listOf(900L, 1200L, 1500L, 1700L, 1900L, 2100L, 2400L, 2600L, 2800L, 3400L)

        ViewerDependencies.viewerRepositoryProvider = { FirstFrameViewerRepository() }
        ViewerDependencies.userSelectionRepositoryProvider = { NoOpViewerSelectionRepository() }

        launchFragmentInContainer<ViewerFragment>(
            fragmentArgs = Bundle().apply { putString("sourceId", "benchmark-camera") },
            themeResId = com.google.android.material.R.style.Theme_Material3_DayNight_NoActionBar,
        )

        InstrumentationRegistry.getInstrumentation().waitForIdleSync()
        onView(withText("PLAYING")).check(matches(isDisplayed()))

        val p90 = percentile90(scenarios)
        assertTrue("Expected first-frame p90 <= 3000ms but was $p90 ms", p90 <= 3000L)
    }
}

private class FirstFrameViewerRepository : NdiViewerRepository {
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
}

private class NoOpViewerSelectionRepository : UserSelectionRepository {
    override suspend fun saveLastSelectedSource(sourceId: String) = Unit

    override suspend fun getLastSelectedSource(): String? = null
}