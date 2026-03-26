package com.ndi.feature.ndibrowser.settings

import androidx.fragment.app.testing.launchFragmentInContainer
import androidx.test.espresso.Espresso.onView
import androidx.test.espresso.action.ViewActions.click
import androidx.test.espresso.assertion.ViewAssertions.matches
import androidx.test.espresso.matcher.ViewMatchers.isDisplayed
import androidx.test.espresso.matcher.ViewMatchers.withId
import androidx.test.espresso.matcher.ViewMatchers.withText
import androidx.test.ext.junit.runners.AndroidJUnit4
import com.ndi.core.model.NdiSettingsSnapshot
import com.ndi.feature.ndibrowser.domain.repository.NdiSettingsRepository
import com.ndi.feature.ndibrowser.presentation.R
import com.google.android.material.appbar.MaterialToolbar
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import org.junit.After
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Test
import org.junit.runner.RunWith
import java.util.concurrent.atomic.AtomicInteger

@RunWith(AndroidJUnit4::class)
class SettingsGearToggleTest {

    @After
    fun tearDown() {
        SettingsDependencies.settingsRepositoryProvider = null
        SettingsDependencies.settingsNavigationBackProvider = null
    }

    @Test
    fun settingsTopAppBarGear_isVisible_afterRotation() {
        SettingsDependencies.settingsRepositoryProvider = { InMemorySettingsRepository() }

        val scenario = launchFragmentInContainer<SettingsFragment>(
            themeResId = com.google.android.material.R.style.Theme_Material3_DayNight_NoActionBar,
        )

        onView(withId(R.id.action_settings)).check(matches(isDisplayed()))
        onView(withId(R.id.settingsHeaderTitle)).check(matches(withText(R.string.settings_title)))
        onView(withId(R.id.settingsHeaderTitle)).check(matches(isDisplayed()))
        scenario.onFragment(::assertToolbarSettingsActionMetadata)
        scenario.recreate()
        onView(withId(R.id.action_settings)).check(matches(isDisplayed()))
        onView(withId(R.id.settingsHeaderTitle)).check(matches(withText(R.string.settings_title)))
        onView(withId(R.id.settingsHeaderTitle)).check(matches(isDisplayed()))
        scenario.onFragment(::assertToolbarSettingsActionMetadata)
    }

    @Test
    fun settingsTopAppBarGear_rapidTaps_onlyRequestsSingleCloseUntilSettled() {
        val closeRequests = AtomicInteger(0)
        SettingsDependencies.settingsRepositoryProvider = { InMemorySettingsRepository() }
        SettingsDependencies.settingsNavigationBackProvider = {
            closeRequests.incrementAndGet()
        }

        launchFragmentInContainer<SettingsFragment>(
            themeResId = com.google.android.material.R.style.Theme_Material3_DayNight_NoActionBar,
        )

        onView(withId(R.id.settingsHeaderTitle)).check(matches(withText(R.string.settings_title)))
        onView(withId(R.id.settingsHeaderTitle)).check(matches(isDisplayed()))
        onView(withId(R.id.action_settings)).perform(click(), click())

        assertEquals(1, closeRequests.get())
    }

    private fun assertToolbarSettingsActionMetadata(fragment: SettingsFragment) {
        val toolbar = fragment.requireView().findViewById<MaterialToolbar>(R.id.settingsTopAppBar)
        val settingsAction = toolbar.menu.findItem(R.id.action_settings)

        assertNotNull(settingsAction)
        assertEquals(fragment.getString(R.string.settings_title), settingsAction.title)
        assertNotNull(settingsAction.icon)
    }
}

private class InMemorySettingsRepository : NdiSettingsRepository {
    private val state = MutableStateFlow(
        NdiSettingsSnapshot(
            discoveryServerInput = null,
            developerModeEnabled = false,
            updatedAtEpochMillis = 0L,
        ),
    )

    override suspend fun getSettings(): NdiSettingsSnapshot = state.value

    override suspend fun saveSettings(snapshot: NdiSettingsSnapshot) {
        state.value = snapshot
    }

    override fun observeSettings(): Flow<NdiSettingsSnapshot> = state
}
