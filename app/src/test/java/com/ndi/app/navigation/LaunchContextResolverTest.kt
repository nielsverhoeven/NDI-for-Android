package com.ndi.app.navigation

import android.content.Intent
import com.ndi.core.model.navigation.LaunchContext
import org.junit.Assert.assertEquals
import org.junit.Test

class LaunchContextResolverTest {

    @Test
    fun nullIntent_resolvesToLauncher() {
        assertEquals(LaunchContext.LAUNCHER, LaunchContextResolver.resolve(null))
    }

    @Test
    fun launcherIntent_resolvesToLauncher() {
        val intent = Intent(Intent.ACTION_MAIN).apply {
            addCategory(Intent.CATEGORY_LAUNCHER)
        }
        assertEquals(LaunchContext.LAUNCHER, LaunchContextResolver.resolve(intent))
    }

    @Test
    fun mainActionWithoutLauncherCategory_resolvesToRecentsRestore() {
        val intent = Intent(Intent.ACTION_MAIN)
        assertEquals(LaunchContext.RECENTS_RESTORE, LaunchContextResolver.resolve(intent))
    }

    @Test
    fun ndiDeepLinkUri_resolvesToDeepLink() {
        val intent = Intent(Intent.ACTION_VIEW, android.net.Uri.parse("ndi://viewer/source-1"))
        assertEquals(LaunchContext.DEEP_LINK, LaunchContextResolver.resolve(intent))
    }

    @Test
    fun isLauncherContext_trueForLauncher() {
        assert(LaunchContextResolver.isLauncherContext(LaunchContext.LAUNCHER))
    }

    @Test
    fun isRecentsRestore_trueForRecentsRestore() {
        assert(LaunchContextResolver.isRecentsRestore(LaunchContext.RECENTS_RESTORE))
    }

    @Test
    fun isDeepLink_trueForDeepLink() {
        assert(LaunchContextResolver.isDeepLink(LaunchContext.DEEP_LINK))
    }
}

