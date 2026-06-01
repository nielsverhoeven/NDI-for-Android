package com.ndi.feature.ndibrowser.source_list

import androidx.core.net.toUri
import androidx.test.espresso.Espresso.onView
import androidx.test.espresso.UiController
import androidx.test.espresso.ViewAction
import androidx.test.espresso.assertion.ViewAssertions.matches
import androidx.test.espresso.assertion.ViewAssertions.doesNotExist
import androidx.test.espresso.matcher.ViewMatchers.isEnabled
import androidx.test.espresso.matcher.ViewMatchers.isClickable
import androidx.test.espresso.matcher.ViewMatchers.isDisplayed
import androidx.test.espresso.matcher.ViewMatchers.withId
import androidx.test.espresso.matcher.ViewMatchers.withTagValue
import androidx.test.espresso.matcher.ViewMatchers.withText
import android.view.View
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
import com.ndi.feature.ndibrowser.source_list.adapter.SourceAdapter
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import org.junit.Assert.assertNotNull
import org.junit.Test
import org.junit.runner.RunWith
import org.hamcrest.Matchers.equalTo
import org.hamcrest.Matcher
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

    @Test
    fun currentDeviceSourceIsNotDisplayed_andViewStreamButtonIsUsed() {
        val snapshots = MutableStateFlow(emptySnapshot())
        SourceListDependencies.discoveryRepositoryProvider = { InstrumentedDiscoveryRepository(snapshots) }
        SourceListDependencies.userSelectionRepositoryProvider = { InstrumentedUserSelectionRepository() }
        SourceListDependencies.viewerNavigationRequestProvider = { sourceId ->
            NavDeepLinkRequest.Builder.fromUri("ndi://viewer/$sourceId".toUri()).build()
        }

        launchFragmentInContainer<SourceListFragment>(themeResId = com.google.android.material.R.style.Theme_Material3_DayNight_NoActionBar)
            .onFragment {
                snapshots.value = mixedSnapshot()
                InstrumentationRegistry.getInstrumentation().waitForIdleSync()
                onView(withText("Camera 1")).check(matches(isDisplayed()))
                onView(withText("This Device")).check(doesNotExist())
                onView(withText(R.string.ndi_source_view_stream)).check(matches(isDisplayed()))
                onView(withText(R.string.ndi_source_start_output)).check(doesNotExist())
            }
    }

    @Test
    fun refreshInProgress_disablesRefreshButton_andShowsAdjacentIndicator() {
        val snapshots = MutableStateFlow(successSnapshot())
        SourceListDependencies.discoveryRepositoryProvider = { InstrumentedDiscoveryRepository(snapshots) }
        SourceListDependencies.userSelectionRepositoryProvider = { InstrumentedUserSelectionRepository() }
        SourceListDependencies.viewerNavigationRequestProvider = { sourceId ->
            NavDeepLinkRequest.Builder.fromUri("ndi://viewer/$sourceId".toUri()).build()
        }

        launchFragmentInContainer<SourceListFragment>(themeResId = com.google.android.material.R.style.Theme_Material3_DayNight_NoActionBar)
            .onFragment {
                snapshots.value = inProgressSnapshot()
                InstrumentationRegistry.getInstrumentation().waitForIdleSync()
                onView(withId(R.id.refreshButton)).check(matches(org.hamcrest.CoreMatchers.not(isEnabled())))
                onView(withId(R.id.progressIndicator)).check(matches(isDisplayed()))
            }
    }

    @Test
    fun sourceListExposesStableUiTestTags() {
        val snapshots = MutableStateFlow(successSnapshot())
        SourceListDependencies.discoveryRepositoryProvider = { InstrumentedDiscoveryRepository(snapshots) }
        SourceListDependencies.userSelectionRepositoryProvider = { InstrumentedUserSelectionRepository() }
        SourceListDependencies.viewerNavigationRequestProvider = { sourceId ->
            NavDeepLinkRequest.Builder.fromUri("ndi://viewer/$sourceId".toUri()).build()
        }

        launchFragmentInContainer<SourceListFragment>(themeResId = com.google.android.material.R.style.Theme_Material3_DayNight_NoActionBar)
            .onFragment {
                InstrumentationRegistry.getInstrumentation().waitForIdleSync()
                onView(withTagValue(equalTo(SourceListScreen.REFRESH_BUTTON_TEST_TAG))).check(matches(isDisplayed()))
                onView(withTagValue(equalTo(SourceListScreen.LOADING_ICON_TEST_TAG))).check(matches(org.hamcrest.CoreMatchers.not(isDisplayed())))
                onView(withTagValue(equalTo(SourceListScreen.REFRESH_ERROR_TEST_TAG))).check(matches(org.hamcrest.CoreMatchers.not(isDisplayed())))
                onView(withTagValue(equalTo(SourceAdapter.SOURCE_ROW_CONTAINER_TEST_TAG))).check(matches(isDisplayed()))
                onView(withTagValue(equalTo(SourceAdapter.VIEW_STREAM_BUTTON_TEST_TAG))).check(matches(isDisplayed()))
            }
    }

    @Test
    fun sourceRowBodyTapTarget_isInertAndNotClickable() {
        val snapshots = MutableStateFlow(successSnapshot())
        SourceListDependencies.discoveryRepositoryProvider = { InstrumentedDiscoveryRepository(snapshots) }
        SourceListDependencies.userSelectionRepositoryProvider = { InstrumentedUserSelectionRepository() }
        SourceListDependencies.viewerNavigationRequestProvider = { sourceId ->
            NavDeepLinkRequest.Builder.fromUri("ndi://viewer/$sourceId".toUri()).build()
        }

        launchFragmentInContainer<SourceListFragment>(themeResId = com.google.android.material.R.style.Theme_Material3_DayNight_NoActionBar)
            .onFragment {
                InstrumentationRegistry.getInstrumentation().waitForIdleSync()
                onView(withId(R.id.sourceRowContainer)).check(matches(org.hamcrest.CoreMatchers.not(isClickable())))
            }
    }

    @Test
    fun refreshControl_isBottomLeft_andIndicatorIsAdjacentWhenRefreshing() {
        val snapshots = MutableStateFlow(successSnapshot())
        SourceListDependencies.discoveryRepositoryProvider = { InstrumentedDiscoveryRepository(snapshots) }
        SourceListDependencies.userSelectionRepositoryProvider = { InstrumentedUserSelectionRepository() }
        SourceListDependencies.viewerNavigationRequestProvider = { sourceId ->
            NavDeepLinkRequest.Builder.fromUri("ndi://viewer/$sourceId".toUri()).build()
        }

        launchFragmentInContainer<SourceListFragment>(themeResId = com.google.android.material.R.style.Theme_Material3_DayNight_NoActionBar)
            .onFragment {
                snapshots.value = inProgressSnapshot()
                InstrumentationRegistry.getInstrumentation().waitForIdleSync()

                onView(withId(R.id.refreshButton)).check(matches(isDisplayed()))
                onView(withId(R.id.progressIndicator)).check(matches(isDisplayed()))
                onView(withId(R.id.refreshButton)).perform(assertBottomLeftAndAdjacentTo(R.id.progressIndicator))
            }
    }

    @Test
    fun refreshButton_reEnablesAfterRefreshCompletes() {
        val snapshots = MutableStateFlow(successSnapshot())
        SourceListDependencies.discoveryRepositoryProvider = { InstrumentedDiscoveryRepository(snapshots) }
        SourceListDependencies.userSelectionRepositoryProvider = { InstrumentedUserSelectionRepository() }
        SourceListDependencies.viewerNavigationRequestProvider = { sourceId ->
            NavDeepLinkRequest.Builder.fromUri("ndi://viewer/$sourceId".toUri()).build()
        }

        launchFragmentInContainer<SourceListFragment>(themeResId = com.google.android.material.R.style.Theme_Material3_DayNight_NoActionBar)
            .onFragment {
                snapshots.value = inProgressSnapshot()
                InstrumentationRegistry.getInstrumentation().waitForIdleSync()
                onView(withId(R.id.refreshButton)).check(matches(org.hamcrest.CoreMatchers.not(isEnabled())))

                snapshots.value = successSnapshot()
                InstrumentationRegistry.getInstrumentation().waitForIdleSync()
                onView(withId(R.id.refreshButton)).check(matches(isEnabled()))
            }
    }
}

private fun assertBottomLeftAndAdjacentTo(adjacentViewId: Int): ViewAction {
    return object : ViewAction {
        override fun getDescription(): String {
            return "assert refresh control is bottom-left and adjacent to loading indicator"
        }

        override fun getConstraints(): Matcher<View> {
            return isDisplayed()
        }

        override fun perform(uiController: UiController, view: View) {
            val root = view.rootView
            val indicator = root.findViewById<View>(adjacentViewId)

            val refreshLocation = IntArray(2)
            val indicatorLocation = IntArray(2)
            view.getLocationOnScreen(refreshLocation)
            indicator.getLocationOnScreen(indicatorLocation)

            val refreshCenterY = refreshLocation[1] + (view.height / 2)
            val indicatorCenterY = indicatorLocation[1] + (indicator.height / 2)

            val isLeftSide = refreshLocation[0] < (root.width / 2)
            val isBottomArea = refreshCenterY > (root.height * 3 / 5)
            val isIndicatorToRight = indicatorLocation[0] > (refreshLocation[0] + view.width)
            val isVerticallyAdjacent = kotlin.math.abs(indicatorCenterY - refreshCenterY) <= (view.height / 2)

            org.junit.Assert.assertTrue("refresh button should be on left side", isLeftSide)
            org.junit.Assert.assertTrue("refresh button should be in bottom area", isBottomArea)
            org.junit.Assert.assertTrue("loading indicator should be adjacent on the right", isIndicatorToRight)
            org.junit.Assert.assertTrue("loading indicator should be vertically aligned", isVerticallyAdjacent)
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

private fun mixedSnapshot(): DiscoverySnapshot {
    return DiscoverySnapshot(
        snapshotId = UUID.randomUUID().toString(),
        startedAtEpochMillis = 1L,
        completedAtEpochMillis = 2L,
        status = DiscoveryStatus.SUCCESS,
        sourceCount = 2,
        sources = listOf(
            com.ndi.core.model.NdiSource(
                sourceId = "device-screen:local",
                displayName = "This Device",
                lastSeenAtEpochMillis = 2L,
            ),
            com.ndi.core.model.NdiSource(
                sourceId = "camera-1",
                displayName = "Camera 1",
                lastSeenAtEpochMillis = 2L,
            ),
        ),
    )
}

private fun inProgressSnapshot(): DiscoverySnapshot {
    return DiscoverySnapshot(
        snapshotId = UUID.randomUUID().toString(),
        startedAtEpochMillis = 3L,
        completedAtEpochMillis = 4L,
        status = DiscoveryStatus.IN_PROGRESS,
        sourceCount = 0,
        sources = emptyList(),
    )
}
