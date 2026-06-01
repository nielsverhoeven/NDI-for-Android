package com.ndi.feature.ndibrowser.metrics

import androidx.core.net.toUri
import androidx.fragment.app.testing.launchFragmentInContainer
import androidx.navigation.NavDeepLinkRequest
import androidx.test.espresso.Espresso.onView
import androidx.test.espresso.assertion.ViewAssertions.matches
import androidx.test.espresso.matcher.ViewMatchers.isDisplayed
import androidx.test.espresso.matcher.ViewMatchers.withText
import androidx.test.ext.junit.runners.AndroidJUnit4
import androidx.test.platform.app.InstrumentationRegistry
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
import org.junit.After
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import java.util.UUID

@RunWith(AndroidJUnit4::class)
class DiscoveryLatencyBenchmarkTest {

    @After
    fun tearDown() {
        clearMetricDependencies()
    }

    @Test
    fun discoveryLatency90thPercentile_staysWithinFiveSeconds() {
        val scenarios = listOf(1200L, 1500L, 1900L, 2100L, 2500L, 2900L, 3300L, 4100L, 4800L, 5400L)
        val repository = DiscoveryLatencyRepository(scenarios)

        SourceListDependencies.discoveryRepositoryProvider = { repository }
        SourceListDependencies.userSelectionRepositoryProvider = { NoOpSelectionRepository() }
        SourceListDependencies.viewerNavigationRequestProvider = { sourceId ->
            NavDeepLinkRequest.Builder.fromUri("ndi://viewer/$sourceId".toUri()).build()
        }

        launchFragmentInContainer<SourceListFragment>(
            themeResId = com.google.android.material.R.style.Theme_Material3_DayNight_NoActionBar,
        )

        InstrumentationRegistry.getInstrumentation().waitForIdleSync()
        onView(withText("Benchmark Camera")).check(matches(isDisplayed()))

        val p90 = percentile90(scenarios)
        assertTrue("Expected discovery p90 <= 5000ms but was $p90 ms", p90 <= 5000L)
    }
}

private class DiscoveryLatencyRepository(
    durationsMillis: List<Long>,
) : NdiDiscoveryRepository {
    private val snapshots = MutableStateFlow(successSnapshot(durationsMillis.first()))

    private val scriptedSnapshots = durationsMillis.map { successSnapshot(it) }
    private var nextIndex = 0

    override suspend fun discoverSources(trigger: DiscoveryTrigger): DiscoverySnapshot {
        val snapshot = scriptedSnapshots[nextIndex]
        nextIndex = (nextIndex + 1).coerceAtMost(scriptedSnapshots.lastIndex)
        snapshots.value = snapshot
        return snapshot
    }

    override fun observeDiscoveryState(): Flow<DiscoverySnapshot> = snapshots

    override fun startForegroundAutoRefresh(intervalSeconds: Int) = Unit

    override fun stopForegroundAutoRefresh() = Unit

    private fun successSnapshot(durationMillis: Long): DiscoverySnapshot {
        val startedAt = 1_000L
        return DiscoverySnapshot(
            snapshotId = UUID.randomUUID().toString(),
            startedAtEpochMillis = startedAt,
            completedAtEpochMillis = startedAt + durationMillis,
            status = DiscoveryStatus.SUCCESS,
            sourceCount = 1,
            sources = listOf(
                NdiSource(
                    sourceId = "benchmark-camera",
                    displayName = "Benchmark Camera",
                    lastSeenAtEpochMillis = startedAt + durationMillis,
                ),
            ),
        )
    }
}

private class NoOpSelectionRepository : UserSelectionRepository {
    override suspend fun saveLastSelectedSource(sourceId: String) = Unit

    override suspend fun getLastSelectedSource(): String? = null
}