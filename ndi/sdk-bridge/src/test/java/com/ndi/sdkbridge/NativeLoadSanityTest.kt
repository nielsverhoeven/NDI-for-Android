package com.ndi.sdkbridge

import org.junit.Assert.assertTrue
import org.junit.Test

class NativeLoadSanityTest {

    @Test
    fun nativeBridgeObject_isSafeToTouchWithoutNativeLibsLoaded() {
        val result = runCatching { NativeNdiBridge.stopReceiver() }
        assertTrue(result.isSuccess)
    }
}
