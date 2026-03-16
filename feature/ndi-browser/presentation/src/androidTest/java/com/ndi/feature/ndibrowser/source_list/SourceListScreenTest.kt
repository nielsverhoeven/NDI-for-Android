package com.ndi.feature.ndibrowser.source_list

import androidx.core.net.toUri
import androidx.test.espresso.Espresso.onView
import androidx.test.espresso.assertion.ViewAssertions.matches
import androidx.test.espresso.matcher.ViewMatchers.isDisplayed
import androidx.test.espresso.matcher.ViewMatchers.withText
import androidx.fragment.app.testing.launchFragmentInContainer
import androidx.navigation.NavDeepLinkRequest
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
import com.ndi.feature.ndibrowser.presentation.R
import com.ndi.core.model.DiscoverySnapshot
import com.ndi.core.model.DiscoveryStatus
import com.ndi.core.model.DiscoveryTrigger
import com.ndi.feature.ndibrowser.domain.repository.NdiDiscoveryRepository
import com.ndi.feature.ndibrowser.domain.repository.UserSelectionRepository
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import org.junit.Assert.assertNotNull
import org.junit.Test
import org.junit.runner.RunWith
import java.util.UUID

@RunWith(AndroidJUnit4::class)
class SourceListScreenTest {

    @Test
    fun fragmentLaunchesForEmptyAndSuccessStates() {
        val snapshots = MutableStateFlow(emptySnapshot())
        SourceListDependencies.discoveryRepositoryProvider = { InstrumentedDiscoveryRepository(snapshots) }
        SourceListDependencies.userSelectionRepositoryProvider = { InstrumentedUserSelectionRepository() }
        SourceListDependencies.viewerNavigationRequestProvider = { sourceId ->
            NavDeepLinkRequest.Builder.fromUri("ndi://viewer/$sourceId".toUri()).build()
        }

        launchFragmentInContainer<SourceListFragment>(themeResId = com.google.android.material.R.style.Theme_Material3_DayNight_NoActionBar)
            .onFragment { fragment ->
                assertNotNull(fragment.requireView())
                onView(withText(R.string.ndi_discovery_empty)).check(matches(isDisplayed()))
                snapshots.value = successSnapshot()
                InstrumentationRegistry.getInstrumentation().waitForIdleSync()
                onView(withText("Camera 1")).check(matches(isDisplayed()))
            }
    }
}

private class InstrumentedDiscoveryRepository(
    private val snapshots: MutableStateFlow<DiscoverySnapshot>,
) : NdiDiscoveryRepository {
    override suspend fun discoverSources(trigger: DiscoveryTrigger): DiscoverySnapshot = snapshots.value

    override fun observeDiscoveryState(): Flow<DiscoverySnapshot> = snapshots

    override fun startForegroundAutoRefresh(intervalSeconds: Int) = Unit

    override fun stopForegroundAutoRefresh() = Unit
}

private class InstrumentedUserSelectionRepository : UserSelectionRepository {
    override suspend fun saveLastSelectedSource(sourceId: String) = Unit

    override suspend fun getLastSelectedSource(): String? = null
}

private fun emptySnapshot(): DiscoverySnapshot {
    return DiscoverySnapshot(
        snapshotId = UUID.randomUUID().toString(),
        startedAtEpochMillis = 0L,
        completedAtEpochMillis = 0L,
        status = DiscoveryStatus.EMPTY,
        sourceCount = 0,
        sources = emptyList(),
    )
}

private fun successSnapshot(): DiscoverySnapshot {
    return DiscoverySnapshot(
        snapshotId = UUID.randomUUID().toString(),
        startedAtEpochMillis = 1L,
        completedAtEpochMillis = 2L,
        status = DiscoveryStatus.SUCCESS,
        sourceCount = 1,
        sources = listOf(
            com.ndi.core.model.NdiSource(
                sourceId = "camera-1",
                displayName = "Camera 1",
                lastSeenAtEpochMillis = 2L,
            ),
        ),
    )
}
