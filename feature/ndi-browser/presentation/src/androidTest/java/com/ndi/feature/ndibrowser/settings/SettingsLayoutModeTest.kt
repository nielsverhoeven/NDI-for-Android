package com.ndi.feature.ndibrowser.settings

import androidx.test.ext.junit.runners.AndroidJUnit4
import com.ndi.core.model.SettingsLayoutMode
import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class SettingsLayoutModeTest {

    @Test
    fun wideWidth_usesWideMode() {
        assertEquals(SettingsLayoutMode.WIDE, SettingsLayoutResolver.resolve(widthDp = 720, isLandscape = false))
    }

    @Test
    fun compactWidth_usesCompactMode() {
        assertEquals(SettingsLayoutMode.COMPACT, SettingsLayoutResolver.resolve(widthDp = 411, isLandscape = true))
    }
}
