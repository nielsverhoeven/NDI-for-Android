package com.ndi.feature.ndibrowser.viewer

import androidx.test.ext.junit.runners.AndroidJUnit4
import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class ViewerTabletUiTest {

    @Test
    fun widthAtTabletBreakpoint_usesTabletViewerLayout() {
        assertEquals(ViewerLayoutMode.TABLET, ViewerAdaptiveLayout.resolve(720))
    }
}
