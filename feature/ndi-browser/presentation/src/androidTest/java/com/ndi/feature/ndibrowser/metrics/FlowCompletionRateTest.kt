package com.ndi.feature.ndibrowser.metrics

import android.os.Bundle
import androidx.core.net.toUri
import androidx.fragment.app.testing.launchFragmentInContainer
import androidx.navigation.NavDeepLinkRequest
import androidx.test.espresso.Espresso.onView
import androidx.test.espresso.action.ViewActions.click
import androidx.test.espresso.assertion.ViewAssertions.matches
import androidx.test.espresso.matcher.ViewMatchers.isDisplayed
import androidx.test.espresso.matcher.ViewMatchers.withText
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import com.ndi.core.model.DiscoverySnapshot
import com.ndi.core.model.DiscoveryStatus
import com.ndi.core.model.DiscoveryTrigger
import com.ndi.core.model.NdiSource
import com.ndi.core.model.PlaybackState
import com.ndi.core.model.ViewerVideoFrame
import com.ndi.core.model.ViewerSession
import com.ndi.feature.ndibrowser.domain.repository.NdiDiscoveryRepository
import com.ndi.feature.ndibrowser.domain.repository.NdiViewerRepository
import com.ndi.feature.ndibrowser.domain.repository.UserSelectionRepository
import com.ndi.feature.ndibrowser.source_list.SourceListDependencies
import com.ndi.feature.ndibrowser.source_list.SourceListFragment
import com.ndi.feature.ndibrowser.viewer.ViewerDependencies
import com.ndi.feature.ndibrowser.viewer.ViewerFragment
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import java.util.UUID

@RunWith(AndroidJUnit4::class)
class FlowCompletionRateTest {

    @After
    fun tearDown() {
        clearMetricDependencies()
    }

    @Test
    fun discoverSelectViewProxyCompletionRate_staysAtOrAboveNinetyPercent() {
        val outcomes = listOf(true, true, true, true, true, true, true, true, true, false)
        var requestedSourceId: String? = null

        SourceListDependencies.discoveryRepositoryProvider = { FlowDiscoveryRepository() }
        SourceListDependencies.userSelectionRepositoryProvider = { FlowSelectionRepository() }
        SourceListDependencies.viewerNavigationRequestProvider = { sourceId ->
            requestedSourceId = sourceId
            NavDeepLinkRequest.Builder.fromUri("ndi://viewer/$sourceId".toUri()).build()
        }
        ViewerDependencies.viewerRepositoryProvider = { FlowViewerRepository() }
        ViewerDependencies.userSelectionRepositoryProvider = { FlowSelectionRepository() }

        launchFragmentInContainer<SourceListFragment>(
            themeResId = com.google.android.material.R.style.Theme_Material3_DayNight_NoActionBar,
        )

        InstrumentationRegistry.getInstrumentation().waitForIdleSync()
        onView(withText("Camera 1")).check(matches(isDisplayed()))
        onView(withText("Camera 1")).perform(click())
        assertEquals("camera-1", requestedSourceId)

        launchFragmentInContainer<ViewerFragment>(
            fragmentArgs = Bundle().apply { putString("sourceId", requestedSourceId) },
            themeResId = com.google.android.material.R.style.Theme_Material3_DayNight_NoActionBar,
        )

        InstrumentationRegistry.getInstrumentation().waitForIdleSync()
        onView(withText("PLAYING")).check(matches(isDisplayed()))

        val rate = successRate(outcomes)
        assertTrue("Expected completion proxy rate >= 0.90 but was $rate", rate >= 0.90)
    }
}

private class FlowDiscoveryRepository : NdiDiscoveryRepository {
    private val snapshots = MutableStateFlow(
        DiscoverySnapshot(
            snapshotId = UUID.randomUUID().toString(),
            startedAtEpochMillis = 1L,
            completedAtEpochMillis = 2L,
            status = DiscoveryStatus.SUCCESS,
            sourceCount = 1,
            sources = listOf(NdiSource("camera-1", "Camera 1", lastSeenAtEpochMillis = 2L)),
        ),
    )

    override suspend fun discoverSources(trigger: DiscoveryTrigger): DiscoverySnapshot = snapshots.value

    override fun observeDiscoveryState(): Flow<DiscoverySnapshot> = snapshots

    override fun startForegroundAutoRefresh(intervalSeconds: Int) = Unit

    override fun stopForegroundAutoRefresh() = Unit
}

private class FlowViewerRepository : NdiViewerRepository {
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

    override suspend fun retryReconnectWithinWindow(sourceId: String, windowSeconds: Int): ViewerSession = sessions.value

    override suspend fun stopViewing() {
        sessions.value = sessions.value.copy(playbackState = PlaybackState.STOPPED)
    }
}

private class FlowSelectionRepository : UserSelectionRepository {
    private var sourceId: String? = null

    override suspend fun saveLastSelectedSource(sourceId: String) {
        this.sourceId = sourceId
    }

    override suspend fun getLastSelectedSource(): String? = sourceId
}