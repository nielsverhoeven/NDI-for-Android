package com.ndi.feature.ndibrowser.settings

import androidx.test.ext.junit.runners.AndroidJUnit4
import com.ndi.core.model.SettingsLayoutMode
import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class SettingsRotationContinuityTest {

    @Test
    fun rotateWideToCompactToWide_layoutModesTransitionPredictably() {
        val wide = SettingsLayoutResolver.resolve(widthDp = 720, isLandscape = true)
        val compact = SettingsLayoutResolver.resolve(widthDp = 411, isLandscape = false)
        val wideAgain = SettingsLayoutResolver.resolve(widthDp = 720, isLandscape = true)

        assertEquals(SettingsLayoutMode.THREE_COLUMN, wide)
        assertEquals(SettingsLayoutMode.COMPACT, compact)
        assertEquals(SettingsLayoutMode.THREE_COLUMN, wideAgain)
    }
}
