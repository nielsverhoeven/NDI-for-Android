package com.ndi.feature.ndibrowser

import androidx.test.ext.junit.runners.AndroidJUnit4
import com.ndi.feature.ndibrowser.source_list.SourceListAdaptiveLayout
import com.ndi.feature.ndibrowser.source_list.SourceListLayoutMode
import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class Api24CompatibilityTest {

    @Test
    fun compactPhoneWidth_remainsSupported() {
        assertEquals(SourceListLayoutMode.COMPACT, SourceListAdaptiveLayout.resolve(360))
    }
}
