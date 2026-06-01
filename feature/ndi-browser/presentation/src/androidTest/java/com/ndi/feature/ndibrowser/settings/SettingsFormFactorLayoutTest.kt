package com.ndi.feature.ndibrowser.settings

import androidx.fragment.app.testing.launchFragmentInContainer
import androidx.test.espresso.Espresso.onView
import androidx.test.espresso.action.ViewActions.click
import androidx.test.espresso.assertion.ViewAssertions.matches
import androidx.test.espresso.matcher.ViewMatchers.isDisplayed
import androidx.test.espresso.matcher.ViewMatchers.isNotChecked
import androidx.test.espresso.matcher.ViewMatchers.withId
import androidx.test.espresso.matcher.ViewMatchers.withText
import androidx.test.ext.junit.runners.AndroidJUnit4
import com.ndi.core.model.NdiSettingsSnapshot
import com.ndi.core.model.SettingsLayoutMode
import com.ndi.feature.ndibrowser.domain.repository.NdiSettingsRepository
import com.ndi.feature.ndibrowser.domain.repository.SettingsLayoutModeResolver
import com.ndi.feature.ndibrowser.presentation.R
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import org.hamcrest.Matchers.not
import org.junit.After
import org.junit.Test
import org.junit.runner.RunWith

/**
 * Instrumentation tests verifying the Settings UI for the three required form
 * factors: Android tablet (wide), phone in portrait (compact / single-pane nav),
 * and phone in landscape (wide side-by-side or compact depending on width).
 *
 * Layout mode is injected via [SettingsDependencies.layoutResolverProvider] so
 * the tests are deterministic regardless of the physical screen of the test device.
 */
@RunWith(AndroidJUnit4::class)
class SettingsFormFactorLayoutTest {

    @After
    fun tearDown() {
        SettingsDependencies.settingsRepositoryProvider = null
        SettingsDependencies.layoutResolverProvider = null
    }

    // ─── Tablet (wide layout) ─────────────────────────────────────────────────

    /**
     * Tablet: the category menu panel and the detail panel must both be visible
     * at the same time, side by side.
     */
    @Test
    fun tablet_wideLayout_menuAndDetailPanelBothVisible() {
        setupWithResolver(wideResolver())

        launchFragmentInContainer<SettingsFragment>(
            themeResId = com.google.android.material.R.style.Theme_Material3_DayNight_NoActionBar,
        )

        onView(withId(R.id.settingsMenuPanel)).check(matches(isDisplayed()))
        onView(withId(R.id.settingsDetailPanel)).check(matches(isDisplayed()))
    }

    /**
     * Tablet: the back arrow toolbar inside the detail pane must NOT be visible
     * in wide mode because both panels are shown simultaneously.
     */
    @Test
    fun tablet_wideLayout_detailToolbarNotVisible() {
        setupWithResolver(wideResolver())

        launchFragmentInContainer<SettingsFragment>(
            themeResId = com.google.android.material.R.style.Theme_Material3_DayNight_NoActionBar,
        )

        onView(withId(R.id.settingsDetailToolbar)).check(matches(not(isDisplayed())))
    }

    /**
     * Tablet: tapping a category updates the detail pane without hiding the menu.
     */
    @Test
    fun tablet_wideLayout_tapCategory_menuRemainsVisible() {
        setupWithResolver(wideResolver())

        launchFragmentInContainer<SettingsFragment>(
            themeResId = com.google.android.material.R.style.Theme_Material3_DayNight_NoActionBar,
        )

        onView(withId(R.id.settingsCategoryAppearance)).perform(click())

        onView(withId(R.id.settingsMenuPanel)).check(matches(isDisplayed()))
        onView(withId(R.id.settingsDetailPanel)).check(matches(isDisplayed()))
        onView(withId(R.id.settingsDetailToolbar)).check(matches(not(isDisplayed())))
    }

    // ─── Phone portrait (compact / single-pane navigation) ────────────────────

    /**
     * Phone portrait: on first open the category menu fills the screen and the
     * detail panel is not yet visible.
     */
    @Test
    fun phone_portrait_compactLayout_showsCategoryMenuOnly() {
        setupWithResolver(compactResolver())

        launchFragmentInContainer<SettingsFragment>(
            themeResId = com.google.android.material.R.style.Theme_Material3_DayNight_NoActionBar,
        )

        onView(withId(R.id.settingsMenuPanel)).check(matches(isDisplayed()))
        onView(withId(R.id.settingsDetailPanel)).check(matches(not(isDisplayed())))
    }

    /**
     * Phone portrait: tapping a category navigates to the detail screen.
     * The menu panel hides and the detail panel (with its back toolbar) appears.
     */
    @Test
    fun phone_portrait_tapCategory_navigatesToDetailScreen() {
        setupWithResolver(compactResolver())

        launchFragmentInContainer<SettingsFragment>(
            themeResId = com.google.android.material.R.style.Theme_Material3_DayNight_NoActionBar,
        )

        onView(withId(R.id.settingsCategoryAppearance)).perform(click())

        onView(withId(R.id.settingsMenuPanel)).check(matches(not(isDisplayed())))
        onView(withId(R.id.settingsDetailPanel)).check(matches(isDisplayed()))
        onView(withId(R.id.settingsDetailToolbar)).check(matches(isDisplayed()))
    }

    /**
     * Phone portrait: the back toolbar shows the selected category name as its title.
     */
    @Test
    fun phone_portrait_detailToolbar_showsCategoryTitle() {
        setupWithResolver(compactResolver())

        launchFragmentInContainer<SettingsFragment>(
            themeResId = com.google.android.material.R.style.Theme_Material3_DayNight_NoActionBar,
        )

        onView(withId(R.id.settingsCategoryAppearance)).perform(click())

        onView(withId(R.id.settingsDetailToolbar)).check(matches(isDisplayed()))
        // Toolbar title contains the category name "Appearance"
        onView(withText("Appearance")).check(matches(isDisplayed()))
    }

    /**
     * Phone portrait: tapping the back arrow in the detail toolbar returns to the
     * category menu (detail collapses, menu reappears).
     */
    @Test
    fun phone_portrait_detailToolbarBack_returnsToMenu() {
        setupWithResolver(compactResolver())

        launchFragmentInContainer<SettingsFragment>(
            themeResId = com.google.android.material.R.style.Theme_Material3_DayNight_NoActionBar,
        )

        // Navigate into a category
        onView(withId(R.id.settingsCategoryGeneral)).perform(click())
        onView(withId(R.id.settingsDetailPanel)).check(matches(isDisplayed()))

        // Press the back arrow in the toolbar
        onView(withId(R.id.settingsDetailToolbar)).perform(click())

        onView(withId(R.id.settingsMenuPanel)).check(matches(isDisplayed()))
        onView(withId(R.id.settingsDetailPanel)).check(matches(not(isDisplayed())))
    }

    /**
     * Phone portrait: each category in the list is navigable (General, Appearance,
     * Discovery, Developer, About all produce the detail screen).
     */
    @Test
    fun phone_portrait_allCategories_navigateToDetail() {
        val categoryViewIds = listOf(
            R.id.settingsCategoryGeneral,
            R.id.settingsCategoryAppearance,
            R.id.settingsCategoryDiscovery,
            R.id.settingsCategoryDeveloper,
            R.id.settingsCategoryAbout,
        )

        for (categoryId in categoryViewIds) {
            setupWithResolver(compactResolver())
            val scenario = launchFragmentInContainer<SettingsFragment>(
                themeResId = com.google.android.material.R.style.Theme_Material3_DayNight_NoActionBar,
            )

            onView(withId(categoryId)).perform(click())
            onView(withId(R.id.settingsDetailPanel)).check(matches(isDisplayed()))
            onView(withId(R.id.settingsDetailToolbar)).check(matches(isDisplayed()))

            scenario.close()
            SettingsDependencies.settingsRepositoryProvider = null
            SettingsDependencies.layoutResolverProvider = null
        }
    }

    // ─── Phone landscape (compact-height) ─────────────────────────────────────

    /**
     * Phone landscape (compact-height profile: narrow phone ≤430dp wide in landscape):
     * the layout stays in COMPACT / single-pane mode so the menu is shown first.
     */
    @Test
    fun phone_landscape_compactHeight_showsCategoryMenuFirst() {
        // isCompactHeightPhoneProfile: landscape AND widthDp ≤ 430 → still COMPACT
        setupWithResolver(compactResolver())

        launchFragmentInContainer<SettingsFragment>(
            themeResId = com.google.android.material.R.style.Theme_Material3_DayNight_NoActionBar,
        )

        onView(withId(R.id.settingsMenuPanel)).check(matches(isDisplayed()))
        onView(withId(R.id.settingsDetailPanel)).check(matches(not(isDisplayed())))
    }

    /**
     * Phone landscape (wide profile: phone ≥600dp wide in landscape, e.g. Galaxy S25 Ultra):
     * switches to wide side-by-side layout identical to tablet behaviour.
     */
    @Test
    fun phone_landscape_wideWidth_showsSideBySideLayout() {
        setupWithResolver(wideResolver())

        launchFragmentInContainer<SettingsFragment>(
            themeResId = com.google.android.material.R.style.Theme_Material3_DayNight_NoActionBar,
        )

        onView(withId(R.id.settingsMenuPanel)).check(matches(isDisplayed()))
        onView(withId(R.id.settingsDetailPanel)).check(matches(isDisplayed()))
        onView(withId(R.id.settingsDetailToolbar)).check(matches(not(isDisplayed())))
    }

    /**
     * Phone landscape (wide): tapping a category keeps both menu and detail visible —
     * no navigation to a separate screen.
     */
    @Test
    fun phone_landscape_wideWidth_tapCategory_noNavigation() {
        setupWithResolver(wideResolver())

        launchFragmentInContainer<SettingsFragment>(
            themeResId = com.google.android.material.R.style.Theme_Material3_DayNight_NoActionBar,
        )

        onView(withId(R.id.settingsCategoryDeveloper)).perform(click())

        onView(withId(R.id.settingsMenuPanel)).check(matches(isDisplayed()))
        onView(withId(R.id.settingsDetailPanel)).check(matches(isDisplayed()))
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private fun setupWithResolver(resolver: SettingsLayoutModeResolver) {
        SettingsDependencies.settingsRepositoryProvider = { FormFactorTestSettingsRepository() }
        SettingsDependencies.layoutResolverProvider = { resolver }
    }

    private fun wideResolver() = object : SettingsLayoutModeResolver {
        override fun resolve(widthDp: Int, isLandscape: Boolean) = SettingsLayoutMode.WIDE
    }

    private fun compactResolver() = object : SettingsLayoutModeResolver {
        override fun resolve(widthDp: Int, isLandscape: Boolean) = SettingsLayoutMode.COMPACT
    }
}

private class FormFactorTestSettingsRepository : NdiSettingsRepository {
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
