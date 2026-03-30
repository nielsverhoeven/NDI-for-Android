package com.ndi.feature.ndibrowser.settings

import android.os.Bundle
import androidx.fragment.app.testing.launchFragmentInContainer
import androidx.test.espresso.Espresso.onView
import androidx.test.espresso.assertion.ViewAssertions.matches
import androidx.test.espresso.matcher.ViewMatchers.isDisplayed
import androidx.test.espresso.matcher.ViewMatchers.withId
import androidx.test.ext.junit.runners.AndroidJUnit4
import com.ndi.core.model.PlaybackState
import com.ndi.core.model.ViewerVideoFrame
import com.ndi.core.model.ViewerSession
import com.ndi.feature.ndibrowser.domain.repository.NdiViewerRepository
import com.ndi.feature.ndibrowser.domain.repository.UserSelectionRepository
import com.ndi.feature.ndibrowser.presentation.R
import com.ndi.feature.ndibrowser.viewer.ViewerDependencies
import com.ndi.feature.ndibrowser.viewer.ViewerFragment
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import org.junit.Test
import org.junit.runner.RunWith
import java.util.UUID

/**
 * T016 — TDD red phase: verifies Settings menu item is accessible on the Viewer screen.
 * Initially fails until T022 adds the toolbar action to ViewerFragment.
 */
@RunWith(AndroidJUnit4::class)
class ViewerSettingsNavigationTest {

    @Test
    fun settingsMenuItemIsVisibleOnViewerScreen() {
        ViewerDependencies.viewerRepositoryProvider = { StubViewerRepo() }
        ViewerDependencies.userSelectionRepositoryProvider = { StubViewerSelectionRepo() }

        launchFragmentInContainer<ViewerFragment>(
            fragmentArgs = Bundle().apply { putString("sourceId", "test-source") },
            themeResId = com.google.android.material.R.style.Theme_Material3_DayNight_NoActionBar,
        )

        onView(withId(R.id.action_settings)).check(matches(isDisplayed()))
    }
}

private class StubViewerRepo : NdiViewerRepository {
    private val session = MutableStateFlow(
        ViewerSession(
            sessionId = UUID.randomUUID().toString(),
            selectedSourceId = "",
            playbackState = PlaybackState.IDLE,
            startedAtEpochMillis = 0L,
        ),
    )

    override suspend fun connectToSource(sourceId: String): ViewerSession = session.value
    override fun observeViewerSession(): Flow<ViewerSession> = session
    override fun getLatestVideoFrame(): ViewerVideoFrame? = null
    override suspend fun retryReconnectWithinWindow(sourceId: String, windowSeconds: Int): ViewerSession = session.value
    override suspend fun stopViewing(): Unit = Unit
}

private class StubViewerSelectionRepo : UserSelectionRepository {
    override suspend fun saveLastSelectedSource(sourceId: String) = Unit
    override suspend fun getLastSelectedSource(): String? = null
}
