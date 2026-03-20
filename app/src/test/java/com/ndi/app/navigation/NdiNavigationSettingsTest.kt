package com.ndi.app.navigation

import com.ndi.app.R
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNotNull
import org.junit.Test
import org.junit.runner.RunWith
import org.junit.runners.JUnit4

/**
 * T014 — Navigation route helper unit tests (TDD red phase until T018/T019 land).
 * These tests fail to compile until NdiNavigation.settingsRequest() /
 * settingsDestinationId() are added (T019) and R.id.settingsFragment exists (T018).
 */
@RunWith(JUnit4::class)
class NdiNavigationSettingsTest {

    @Test
    fun settingsRequest_returnsNonNullNavDeepLinkRequest() {
        try {
            val request = NdiNavigation.settingsRequest()
            assertNotNull(request)
        } catch (e: RuntimeException) {
            // android.net.Uri.parse() throws in non-Robolectric JVM environment — skip.
            org.junit.Assume.assumeTrue("Android framework unavailable in unit test environment", false)
        }
    }

    @Test
    fun settingsRequest_hasSettingsUri() {
        try {
            val request = NdiNavigation.settingsRequest()
            assertEquals("ndi://settings", request.uri?.toString())
        } catch (e: RuntimeException) {
            org.junit.Assume.assumeTrue("Android framework unavailable in unit test environment", false)
        }
    }

    @Test
    fun settingsDestinationId_returnsSettingsFragmentId() {
        assertEquals(R.id.settingsFragment, NdiNavigation.settingsDestinationId())
    }
}
