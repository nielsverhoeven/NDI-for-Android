package com.ndi.feature.ndibrowser.source_list

import androidx.core.net.toUri
import androidx.navigation.NavDeepLinkRequest
import androidx.test.core.app.ActivityScenario
import androidx.test.espresso.Espresso.onView
import androidx.test.espresso.action.ViewActions.click
import androidx.test.espresso.assertion.ViewAssertions.matches
import androidx.test.espresso.matcher.ViewMatchers.isDisplayed
import androidx.test.espresso.matcher.ViewMatchers.withId
import androidx.test.espresso.matcher.ViewMatchers.withText
import androidx.test.ext.junit.runners.AndroidJUnit4
import com.ndi.app.MainActivity
import com.ndi.app.R
import com.ndi.core.model.DiscoverySnapshot
import com.ndi.core.model.DiscoveryStatus
import com.ndi.core.model.DiscoveryTrigger
import com.ndi.feature.ndibrowser.domain.repository.NdiDiscoveryRepository
import com.ndi.feature.ndibrowser.domain.repository.UserSelectionRepository
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.flowOf
import org.junit.Test
import org.junit.runner.RunWith
import java.util.UUID

@RunWith(AndroidJUnit4::class)
class SourceListFallbackWarningTest {

    @Test
    fun fallbackWarningFlowEmission_makesWarningVisible() {
        val warningText = "Discovery server unavailable. Falling back to default."

        SourceListDependencies.discoveryRepositoryProvider = { AndroidTestDiscoveryRepository() }
        SourceListDependencies.userSelectionRepositoryProvider = { AndroidTestSelectionRepository() }
        SourceListDependencies.viewerNavigationRequestProvider = { sourceId ->
            NavDeepLinkRequest.Builder.fromUri("ndi://viewer/$sourceId".toUri()).build()
        }
        SourceListDependencies.outputNavigationRequestProvider = { sourceId ->
            NavDeepLinkRequest.Builder.fromUri("ndi://output/$sourceId".toUri()).build()
        }
        SourceListDependencies.fallbackWarningProvider = { flowOf(warningText) }

        ActivityScenario.launch(MainActivity::class.java).use {
            onView(withId(R.id.streamFragment)).perform(click())
            onView(withText(warningText)).check(matches(isDisplayed()))
        }
    }
}

private class AndroidTestDiscoveryRepository : NdiDiscoveryRepository {
    private val snapshots = MutableStateFlow(
        DiscoverySnapshot(
            snapshotId = UUID.randomUUID().toString(),
            startedAtEpochMillis = 0L,
            completedAtEpochMillis = 0L,
            status = DiscoveryStatus.EMPTY,
            sourceCount = 0,
            sources = emptyList(),
        ),
    )

    override suspend fun discoverSources(trigger: DiscoveryTrigger): DiscoverySnapshot = snapshots.value

    override fun observeDiscoveryState(): Flow<DiscoverySnapshot> = snapshots

    override fun startForegroundAutoRefresh(intervalSeconds: Int) = Unit

    override fun stopForegroundAutoRefresh() = Unit
}

private class AndroidTestSelectionRepository : UserSelectionRepository {
    override suspend fun saveLastSelectedSource(sourceId: String) = Unit

    override suspend fun getLastSelectedSource(): String? = null
}