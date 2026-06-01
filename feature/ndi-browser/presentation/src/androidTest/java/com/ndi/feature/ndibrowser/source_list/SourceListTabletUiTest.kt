package com.ndi.feature.ndibrowser.source_list

import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class SourceListTabletUiTest {

    @Test
    fun widthAtTabletBreakpoint_usesExpandedLayoutMode() {
        assertEquals(SourceListLayoutMode.EXPANDED, SourceListAdaptiveLayout.resolve(700))
    }
}
