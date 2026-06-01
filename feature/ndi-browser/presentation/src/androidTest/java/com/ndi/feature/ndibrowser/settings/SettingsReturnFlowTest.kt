package com.ndi.feature.ndibrowser.settings

import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class SettingsReturnFlowTest {

    @Test
    fun wideLayoutResolver_remainsThreePaneForSettingsReturn() {
        val modeBefore = SettingsLayoutResolver.resolve(widthDp = 720, isLandscape = true)
        val modeAfter = SettingsLayoutResolver.resolve(widthDp = 720, isLandscape = true)

        assertEquals(modeBefore, modeAfter)
    }
}
