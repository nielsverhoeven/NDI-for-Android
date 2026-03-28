package com.ndi.feature.ndibrowser.settings

import androidx.fragment.app.testing.launchFragmentInContainer
import androidx.test.espresso.Espresso.onView
import androidx.test.espresso.assertion.ViewAssertions.matches
import androidx.test.espresso.matcher.ViewMatchers.isDisplayed
import androidx.test.espresso.matcher.ViewMatchers.withId
import androidx.test.ext.junit.runners.AndroidJUnit4
import com.ndi.feature.ndibrowser.presentation.R
import com.ndi.core.model.NdiSettingsSnapshot
import com.ndi.feature.ndibrowser.domain.repository.NdiSettingsRepository
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.MutableStateFlow
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class SettingsThreePaneUiTest {

    @Test
    fun settingsFragment_containsThreePaneViewHierarchy() {
        SettingsDependencies.settingsRepositoryProvider = { AndroidTestSettingsRepository() }
        launchFragmentInContainer<SettingsFragment>(
            themeResId = com.google.android.material.R.style.Theme_Material3_DayNight_NoActionBar,
        )

        onView(withId(R.id.settingsThreePaneContainer)).check(matches(isDisplayed()))
    }
}

private class AndroidTestSettingsRepository : NdiSettingsRepository {
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
