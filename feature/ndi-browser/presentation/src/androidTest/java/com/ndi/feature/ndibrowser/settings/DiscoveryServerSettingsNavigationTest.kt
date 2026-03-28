package com.ndi.feature.ndibrowser.settings

import androidx.fragment.app.testing.launchFragmentInContainer
import androidx.test.espresso.Espresso.onView
import androidx.test.espresso.action.ViewActions.click
import androidx.test.espresso.action.ViewActions.replaceText
import androidx.test.espresso.assertion.ViewAssertions.matches
import androidx.test.espresso.matcher.ViewMatchers.isDisplayed
import androidx.test.espresso.matcher.ViewMatchers.withId
import androidx.test.espresso.matcher.ViewMatchers.withText
import androidx.test.ext.junit.runners.AndroidJUnit4
import com.ndi.feature.ndibrowser.presentation.R
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith

/**
 * Instrumentation test: opens the DiscoveryServerSettingsFragment and verifies
 * that saving a host-only server shows it in the list with port 5959.
 *
 * NOTE: This test is currently FAILING because DiscoveryServerSettingsFragment
 * and its layout do not exist yet. It is written first per TDD requirements.
 */
@RunWith(AndroidJUnit4::class)
class DiscoveryServerSettingsNavigationTest {

    @Before
    fun setUp() {
        // Wire fake repository before launching the fragment
        SettingsDependencies.discoveryServerRepositoryProvider = {
            FakeAndroidDiscoveryServerRepository()
        }
    }

    @Test
    fun opensDiscoveryServerSubmenuAndDisplaysAddForm() {
        launchFragmentInContainer<DiscoveryServerSettingsFragment>(
            themeResId = com.google.android.material.R.style.Theme_Material3_DayNight,
        )
        onView(withId(R.id.discoveryHostInput)).check(matches(isDisplayed()))
        onView(withId(R.id.discoveryPortInput)).check(matches(isDisplayed()))
    }

    @Test
    fun saveHostOnlyServerAppearsInListWithDefaultPort5959() {
        launchFragmentInContainer<DiscoveryServerSettingsFragment>(
            themeResId = com.google.android.material.R.style.Theme_Material3_DayNight,
        )
        onView(withId(R.id.discoveryHostInput)).perform(replaceText("ndi-server.local"))
        onView(withId(R.id.discoveryPortInput)).perform(replaceText(""))
        onView(withId(R.id.addDiscoveryServerButton)).perform(click())
        onView(withText("ndi-server.local:5959")).check(matches(isDisplayed()))
    }

    @Test
    fun saveWithNoHostShowsValidationError() {
        launchFragmentInContainer<DiscoveryServerSettingsFragment>(
            themeResId = com.google.android.material.R.style.Theme_Material3_DayNight,
        )
        onView(withId(R.id.addDiscoveryServerButton)).perform(click())
        onView(withId(R.id.discoveryValidationError)).check(matches(isDisplayed()))
    }
}

/** Minimal Android-context-compatible fake for instrumentation tests. */
class FakeAndroidDiscoveryServerRepository : com.ndi.feature.ndibrowser.domain.repository.DiscoveryServerRepository {
    private val entries = mutableListOf<com.ndi.core.model.DiscoveryServerEntry>()
    private val _flow = kotlinx.coroutines.flow.MutableStateFlow<List<com.ndi.core.model.DiscoveryServerEntry>>(emptyList())

    override fun observeServers() = _flow

    override suspend fun addServer(hostOrIp: String, portInput: String): com.ndi.core.model.DiscoveryServerEntry {
        val host = hostOrIp.trim()
        if (host.isBlank()) throw IllegalArgumentException("Host required")
        val port = if (portInput.isBlank()) com.ndi.core.model.DEFAULT_DISCOVERY_SERVER_PORT else portInput.toInt()
        val entry = com.ndi.core.model.DiscoveryServerEntry(
            id = java.util.UUID.randomUUID().toString(),
            hostOrIp = host, port = port, enabled = true,
            orderIndex = entries.size,
            createdAtEpochMillis = 0L, updatedAtEpochMillis = 0L,
        )
        entries.add(entry)
        _flow.value = entries.toList()
        return entry
    }

    override suspend fun updateServer(id: String, hostOrIp: String, portInput: String) =
        throw UnsupportedOperationException()
    override suspend fun removeServer(id: String) { entries.removeAll { it.id == id }; _flow.value = entries.toList() }
    override suspend fun setServerEnabled(id: String, enabled: Boolean) =
        throw UnsupportedOperationException()
    override suspend fun reorderServers(idsInOrder: List<String>) = emptyList<com.ndi.core.model.DiscoveryServerEntry>()
    override suspend fun resolveActiveDiscoveryTarget() =
        throw UnsupportedOperationException()
}
