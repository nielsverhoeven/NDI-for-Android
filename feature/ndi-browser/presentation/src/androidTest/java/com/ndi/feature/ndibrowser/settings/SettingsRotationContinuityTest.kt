package com.ndi.feature.ndibrowser.settings

import androidx.test.ext.junit.runners.AndroidJUnit4
import com.ndi.core.model.SettingsLayoutMode
import org.junit.Ignore
import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
@Ignore("Converted to Playwright primary orientation coverage in testing/e2e/tests/027-mobile-settings-parity.spec.ts")
class SettingsRotationContinuityTest {

    @Test
    fun rotateWideToCompactToWide_layoutModesTransitionPredictably() {
        val wide = SettingsLayoutResolver.resolve(widthDp = 720, isLandscape = true)
        val compact = SettingsLayoutResolver.resolve(widthDp = 411, isLandscape = false)
        val wideAgain = SettingsLayoutResolver.resolve(widthDp = 720, isLandscape = true)

        assertEquals(SettingsLayoutMode.WIDE, wide)
        assertEquals(SettingsLayoutMode.COMPACT, compact)
        assertEquals(SettingsLayoutMode.WIDE, wideAgain)
    }
}
