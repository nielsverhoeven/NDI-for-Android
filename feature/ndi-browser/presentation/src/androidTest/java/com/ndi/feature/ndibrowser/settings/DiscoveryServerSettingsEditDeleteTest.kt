package com.ndi.feature.ndibrowser.settings

import androidx.fragment.app.testing.launchFragmentInContainer
import androidx.test.espresso.Espresso.onView
import androidx.test.espresso.action.ViewActions.click
import androidx.test.espresso.action.ViewActions.replaceText
import androidx.test.espresso.assertion.ViewAssertions.doesNotExist
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
 * Instrumentation test: edit, delete, and drag-reorder interactions.
 *
 * NOTE: FAILING — DiscoveryServerSettingsFragment edit/delete not yet implemented.
 */
@RunWith(AndroidJUnit4::class)
class DiscoveryServerSettingsEditDeleteTest {

    private lateinit var fakeRepo: FakeAndroidDiscoveryServerRepository

    @Before
    fun setUp() {
        fakeRepo = FakeAndroidDiscoveryServerRepository()
        SettingsDependencies.discoveryServerRepositoryProvider = { fakeRepo }
    }

    @Test
    fun editServerPortAndPersists() {
        val scenario = launchFragmentInContainer<DiscoveryServerSettingsFragment>(
            themeResId = com.google.android.material.R.style.Theme_Material3_DayNight,
        )
        onView(withId(R.id.discoveryHostInput)).perform(replaceText("edit-me.local"))
        onView(withId(R.id.addDiscoveryServerButton)).perform(click())
        onView(withText("edit-me.local:5959")).check(matches(isDisplayed()))

        // Tap edit button in the row
        onView(withId(R.id.editServerButton)).perform(click())
        onView(withId(R.id.discoveryPortInput)).perform(replaceText("7000"))
        onView(withId(R.id.saveEditButton)).perform(click())
        onView(withText("edit-me.local:7000")).check(matches(isDisplayed()))

        scenario.recreate()
        onView(withText("edit-me.local:7000")).check(matches(isDisplayed()))
    }

    @Test
    fun deleteServerAndItDisappearsAndDoesNotReturnAfterRecreate() {
        val scenario = launchFragmentInContainer<DiscoveryServerSettingsFragment>(
            themeResId = com.google.android.material.R.style.Theme_Material3_DayNight,
        )
        onView(withId(R.id.discoveryHostInput)).perform(replaceText("delete-me.local"))
        onView(withId(R.id.addDiscoveryServerButton)).perform(click())
        onView(withText("delete-me.local:5959")).check(matches(isDisplayed()))

        onView(withId(R.id.deleteServerButton)).perform(click())
        // Dismiss any confirmation dialog
        try {
            onView(withText("Delete")).perform(click())
        } catch (_: Exception) {}

        onView(withText("delete-me.local:5959")).check(doesNotExist())

        scenario.recreate()
        onView(withText("delete-me.local:5959")).check(doesNotExist())
    }

    @Test
    fun editToDuplicateShowsValidationError() {
        launchFragmentInContainer<DiscoveryServerSettingsFragment>(
            themeResId = com.google.android.material.R.style.Theme_Material3_DayNight,
        )
        onView(withId(R.id.discoveryHostInput)).perform(replaceText("alpha.local"))
        onView(withId(R.id.addDiscoveryServerButton)).perform(click())
        onView(withId(R.id.discoveryHostInput)).perform(replaceText("beta.local"))
        onView(withId(R.id.discoveryPortInput)).perform(replaceText("5960"))
        onView(withId(R.id.addDiscoveryServerButton)).perform(click())

        // Edit beta to conflict with alpha
        onView(withText("beta.local:5960")).perform(click())
        onView(withId(R.id.discoveryHostInput)).perform(replaceText("alpha.local"))
        onView(withId(R.id.discoveryPortInput)).perform(replaceText("5959"))
        onView(withId(R.id.saveEditButton)).perform(click())
        onView(withId(R.id.discoveryValidationError)).check(matches(isDisplayed()))
    }
}
