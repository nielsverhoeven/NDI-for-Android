package com.ndi.feature.ndibrowser.viewer

import androidx.core.net.toUri
import androidx.fragment.app.testing.launchFragmentInContainer
import androidx.navigation.NavDeepLinkRequest
import androidx.test.espresso.Espresso.onView
import androidx.test.espresso.action.ViewActions.click
import androidx.test.espresso.assertion.ViewAssertions.matches
import androidx.test.espresso.matcher.ViewMatchers.isDisplayed
import androidx.test.espresso.matcher.ViewMatchers.withText
import androidx.test.ext.junit.runners.AndroidJUnit4
import com.ndi.core.model.DiscoverySnapshot
import com.ndi.core.model.DiscoveryStatus
import com.ndi.core.model.DiscoveryTrigger
import com.ndi.core.model.NdiSource
import com.ndi.feature.ndibrowser.domain.repository.NdiDiscoveryRepository
import com.ndi.feature.ndibrowser.domain.repository.UserSelectionRepository
import com.ndi.feature.ndibrowser.source_list.SourceListDependencies
import com.ndi.feature.ndibrowser.source_list.SourceListFragment
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.runner.RunWith
import java.util.UUID

@RunWith(AndroidJUnit4::class)
class ViewerNavigationTest {

    @Test
    fun clickingSource_emitsViewerNavigationRequest() {
        val snapshots = MutableStateFlow(
            DiscoverySnapshot(
                snapshotId = UUID.randomUUID().toString(),
                startedAtEpochMillis = 1L,
                completedAtEpochMillis = 2L,
                status = DiscoveryStatus.SUCCESS,
                sourceCount = 1,
                sources = listOf(NdiSource("camera-1", "Camera 1", lastSeenAtEpochMillis = 2L)),
            ),
        )
        var requestedSourceId: String? = null
        SourceListDependencies.discoveryRepositoryProvider = { TestDiscoveryRepository(snapshots) }
        SourceListDependencies.userSelectionRepositoryProvider = { TestSelectionRepository() }
        SourceListDependencies.viewerNavigationRequestProvider = { sourceId ->
            requestedSourceId = sourceId
            NavDeepLinkRequest.Builder.fromUri("ndi://viewer/$sourceId".toUri()).build()
        }

        launchFragmentInContainer<SourceListFragment>(themeResId = com.google.android.material.R.style.Theme_Material3_DayNight_NoActionBar)

        onView(withText("Camera 1")).check(matches(isDisplayed()))
        onView(withText("Camera 1")).perform(click())

        assertEquals("camera-1", requestedSourceId)
    }
}

private class TestDiscoveryRepository(
    private val snapshots: MutableStateFlow<DiscoverySnapshot>,
) : NdiDiscoveryRepository {
    override suspend fun discoverSources(trigger: DiscoveryTrigger): DiscoverySnapshot = snapshots.value

    override fun observeDiscoveryState(): Flow<DiscoverySnapshot> = snapshots

    override fun startForegroundAutoRefresh(intervalSeconds: Int) = Unit

    override fun stopForegroundAutoRefresh() = Unit
}

private class TestSelectionRepository : UserSelectionRepository {
    private var sourceId: String? = null

    override suspend fun saveLastSelectedSource(sourceId: String) {
        this.sourceId = sourceId
    }

    override suspend fun getLastSelectedSource(): String? = sourceId
}
