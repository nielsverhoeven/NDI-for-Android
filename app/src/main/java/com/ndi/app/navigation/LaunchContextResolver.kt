package com.ndi.app.navigation

import android.content.Intent
import com.ndi.core.model.navigation.LaunchContext

/**
 * Resolves the launch context from the incoming intent so that top-level navigation
 * can make deterministic routing decisions.
 *
 * Rules (per spec 003 FR-004a):
 * - LAUNCHER intent → HOME
 * - Recents/task-restore (null action or same-task resume) → last saved destination
 * - Deep link (custom URI scheme) → DEEP_LINK
 * - In-app switch → IN_APP_SWITCH
 */
object LaunchContextResolver {

    fun resolve(intent: Intent?): LaunchContext {
        if (intent == null) return LaunchContext.LAUNCHER

        val action = intent.action
        val data = intent.data

        return when {
            data != null && (data.scheme == "ndi" || data.scheme == "android-app") ->
                LaunchContext.DEEP_LINK

            action == Intent.ACTION_MAIN && intent.hasCategory(Intent.CATEGORY_LAUNCHER) ->
                LaunchContext.LAUNCHER

            action == null ->
                LaunchContext.RECENTS_RESTORE

            action == Intent.ACTION_MAIN ->
                LaunchContext.RECENTS_RESTORE

            else ->
                LaunchContext.IN_APP_SWITCH
        }
    }

    fun isLauncherContext(context: LaunchContext): Boolean =
        context == LaunchContext.LAUNCHER

    fun isRecentsRestore(context: LaunchContext): Boolean =
        context == LaunchContext.RECENTS_RESTORE

    fun isDeepLink(context: LaunchContext): Boolean =
        context == LaunchContext.DEEP_LINK
}

