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
 * Instrumentation test: multiple servers persist display across Fragment re-creation.
 *
 * NOTE: This test is currently FAILING — DiscoveryServerSettingsFragment not yet implemented.
 */
@RunWith(AndroidJUnit4::class)
class DiscoveryServerSettingsPersistenceTest {

    private lateinit var fakeRepo: FakeAndroidDiscoveryServerRepository

    @Before
    fun setUp() {
        fakeRepo = FakeAndroidDiscoveryServerRepository()
        SettingsDependencies.discoveryServerRepositoryProvider = { fakeRepo }
    }

    @Test
    fun threeServersPersistInOrderedList() {
        val scenario = launchFragmentInContainer<DiscoveryServerSettingsFragment>(
            themeResId = com.google.android.material.R.style.Theme_Material3_DayNight,
        )

        onView(withId(R.id.discoveryHostInput)).perform(replaceText("alpha.local"))
        onView(withId(R.id.addDiscoveryServerButton)).perform(click())
        onView(withId(R.id.discoveryHostInput)).perform(replaceText("beta.local"))
        onView(withId(R.id.discoveryPortInput)).perform(replaceText("5960"))
        onView(withId(R.id.addDiscoveryServerButton)).perform(click())
        onView(withId(R.id.discoveryHostInput)).perform(replaceText("gamma.local"))
        onView(withId(R.id.discoveryPortInput)).perform(replaceText("6000"))
        onView(withId(R.id.addDiscoveryServerButton)).perform(click())

        // Verify all three appear
        onView(withText("alpha.local:5959")).check(matches(isDisplayed()))
        onView(withText("beta.local:5960")).check(matches(isDisplayed()))
        onView(withText("gamma.local:6000")).check(matches(isDisplayed()))

        // Recreate fragment (simulates configuration change / relaunch)
        scenario.recreate()

        onView(withText("alpha.local:5959")).check(matches(isDisplayed()))
        onView(withText("beta.local:5960")).check(matches(isDisplayed()))
        onView(withText("gamma.local:6000")).check(matches(isDisplayed()))
    }

    @Test
    fun duplicateServerShowsErrorMessage() {
        launchFragmentInContainer<DiscoveryServerSettingsFragment>(
            themeResId = com.google.android.material.R.style.Theme_Material3_DayNight,
        )
        onView(withId(R.id.discoveryHostInput)).perform(replaceText("alpha.local"))
        onView(withId(R.id.addDiscoveryServerButton)).perform(click())
        // Try adding same host again
        onView(withId(R.id.discoveryHostInput)).perform(replaceText("alpha.local"))
        onView(withId(R.id.addDiscoveryServerButton)).perform(click())
        onView(withId(R.id.discoveryValidationError)).check(matches(isDisplayed()))
    }
}
