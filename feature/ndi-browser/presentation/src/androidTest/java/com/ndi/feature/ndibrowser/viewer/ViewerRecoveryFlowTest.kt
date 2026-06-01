package com.ndi.feature.ndibrowser.viewer

import android.os.Bundle
import androidx.fragment.app.testing.launchFragmentInContainer
import androidx.test.espresso.Espresso.onView
import androidx.test.espresso.assertion.ViewAssertions.matches
import androidx.test.espresso.matcher.ViewMatchers.isDisplayed
import androidx.test.espresso.matcher.ViewMatchers.withText
import androidx.test.ext.junit.runners.AndroidJUnit4
import com.ndi.core.model.PlaybackState
import com.ndi.core.model.ViewerVideoFrame
import com.ndi.core.model.ViewerSession
import com.ndi.feature.ndibrowser.domain.repository.NdiViewerRepository
import com.ndi.feature.ndibrowser.domain.repository.UserSelectionRepository
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import org.junit.Test
import org.junit.runner.RunWith
import java.util.UUID

@RunWith(AndroidJUnit4::class)
class ViewerRecoveryFlowTest {

    @Test
    fun interruptionDisplaysRecoveryMessageAndActions() {
        ViewerDependencies.viewerRepositoryProvider = { RecoveryViewerRepository() }
        ViewerDependencies.userSelectionRepositoryProvider = { RecoverySelectionRepository() }

        launchFragmentInContainer<ViewerFragment>(
            fragmentArgs = Bundle().apply { putString("sourceId", "camera-11") },
            themeResId = com.google.android.material.R.style.Theme_Material3_DayNight_NoActionBar,
        )

        onView(withText("Playback interrupted")).check(matches(isDisplayed()))
        onView(withText("Retry")).check(matches(isDisplayed()))
    }
}

private class RecoveryViewerRepository : NdiViewerRepository {
    private val sessions = MutableStateFlow(
        ViewerSession(
            sessionId = UUID.randomUUID().toString(),
            selectedSourceId = "",
            playbackState = PlaybackState.IDLE,
            startedAtEpochMillis = 0L,
        ),
    )

    override suspend fun connectToSource(sourceId: String): ViewerSession {
        sessions.value = ViewerSession(
            sessionId = UUID.randomUUID().toString(),
            selectedSourceId = sourceId,
            playbackState = PlaybackState.STOPPED,
            interruptionReason = "Playback interrupted",
            startedAtEpochMillis = 1L,
            endedAtEpochMillis = 2L,
        )
        return sessions.value
    }

    override fun observeViewerSession(): Flow<ViewerSession> = sessions

    override fun getLatestVideoFrame(): ViewerVideoFrame? = null

    override suspend fun retryReconnectWithinWindow(sourceId: String, windowSeconds: Int): ViewerSession = sessions.value

    override suspend fun stopViewing() = Unit
}

private class RecoverySelectionRepository : UserSelectionRepository {
    override suspend fun saveLastSelectedSource(sourceId: String) = Unit

    override suspend fun getLastSelectedSource(): String? = null
}