package com.ndi.feature.ndibrowser.settings

import androidx.fragment.app.testing.launchFragmentInContainer
import androidx.test.espresso.Espresso.onView
import androidx.test.espresso.action.ViewActions.click
import androidx.test.espresso.action.ViewActions.replaceText
import androidx.test.espresso.assertion.ViewAssertions.matches
import androidx.test.espresso.matcher.ViewMatchers.isChecked
import androidx.test.espresso.matcher.ViewMatchers.isDisplayed
import androidx.test.espresso.matcher.ViewMatchers.isNotChecked
import androidx.test.espresso.matcher.ViewMatchers.withId
import androidx.test.espresso.matcher.ViewMatchers.withText
import androidx.test.ext.junit.runners.AndroidJUnit4
import com.ndi.feature.ndibrowser.presentation.R
import org.junit.Before
import org.junit.Test
import org.junit.runner.RunWith

/**
 * Instrumentation test: per-server toggle state persists after Fragment re-creation.
 *
 * NOTE: This test is FAILING — DiscoveryServerSettingsFragment not yet fully implemented.
 */
@RunWith(AndroidJUnit4::class)
class DiscoveryServerSettingsTogglePersistenceTest {

    private lateinit var fakeRepo: FakeAndroidDiscoveryServerRepository

    @Before
    fun setUp() {
        fakeRepo = FakeAndroidDiscoveryServerRepository()
        SettingsDependencies.discoveryServerRepositoryProvider = { fakeRepo }
    }

    @Test
    fun disabledTogglePersistsAfterRecreate() {
        val scenario = launchFragmentInContainer<DiscoveryServerSettingsFragment>(
            themeResId = com.google.android.material.R.style.Theme_Material3_DayNight,
        )
        // Add a server
        onView(withId(R.id.discoveryHostInput)).perform(replaceText("toggle-server.local"))
        onView(withId(R.id.addDiscoveryServerButton)).perform(click())

        // Toggle the switch off
        onView(withId(R.id.serverEnabledSwitch)).perform(click())
        onView(withId(R.id.serverEnabledSwitch)).check(matches(isNotChecked()))

        // Recreate fragment
        scenario.recreate()

        // Toggle should still be off
        onView(withId(R.id.serverEnabledSwitch)).check(matches(isNotChecked()))
    }

    @Test
    fun reEnabledTogglePersistsAfterRecreate() {
        val scenario = launchFragmentInContainer<DiscoveryServerSettingsFragment>(
            themeResId = com.google.android.material.R.style.Theme_Material3_DayNight,
        )
        onView(withId(R.id.discoveryHostInput)).perform(replaceText("toggle-server.local"))
        onView(withId(R.id.addDiscoveryServerButton)).perform(click())

        // Toggle off then on
        onView(withId(R.id.serverEnabledSwitch)).perform(click())
        onView(withId(R.id.serverEnabledSwitch)).perform(click())
        onView(withId(R.id.serverEnabledSwitch)).check(matches(isChecked()))

        scenario.recreate()
        onView(withId(R.id.serverEnabledSwitch)).check(matches(isChecked()))
    }
}
