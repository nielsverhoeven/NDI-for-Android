package com.ndi.app.navigation

import androidx.test.ext.junit.runners.AndroidJUnit4
import com.ndi.core.model.SettingsMainDestination
import org.junit.Assert.assertEquals
import org.junit.Test
import org.junit.runner.RunWith

@RunWith(AndroidJUnit4::class)
class SettingsMainNavigationRoutingTest {

    @Test
    fun settingsMainNavigationRequest_routesToExpectedUris() {
        assertEquals("ndi://home", NdiNavigation.settingsMainNavigationRequest(SettingsMainDestination.HOME).uri.toString())
        assertEquals("ndi://stream", NdiNavigation.settingsMainNavigationRequest(SettingsMainDestination.STREAM).uri.toString())
        assertEquals("ndi://view", NdiNavigation.settingsMainNavigationRequest(SettingsMainDestination.VIEW).uri.toString())
        assertEquals("ndi://settings", NdiNavigation.settingsMainNavigationRequest(SettingsMainDestination.SETTINGS).uri.toString())
    }
}
