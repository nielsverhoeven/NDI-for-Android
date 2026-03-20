package com.ndi.app.navigation

import android.os.Bundle
import androidx.core.net.toUri
import androidx.navigation.NavDeepLinkRequest
import com.ndi.app.R
import com.ndi.core.model.navigation.TopLevelDestination

object NdiNavigation {

    fun viewerRequest(sourceId: String): NavDeepLinkRequest {
        return NavDeepLinkRequest.Builder
            .fromUri("ndi://viewer/$sourceId".toUri())
            .build()
    }

    fun outputRequest(sourceId: String): NavDeepLinkRequest {
        return NavDeepLinkRequest.Builder
            .fromUri("ndi://output/$sourceId".toUri())
            .build()
    }

    fun sourceIdFrom(args: Bundle?): String {
        return args?.getString("sourceId").orEmpty()
    }

    fun outputSourceIdFrom(args: Bundle?): String {
        return args?.getString("sourceId").orEmpty()
    }

    // ---- Top-level destination route IDs (spec 003) ----

    fun homeDestinationId(): Int = R.id.homeDashboardFragment

    fun streamDestinationId(): Int = R.id.streamFragment

    fun viewDestinationId(): Int = R.id.viewFragment

    fun topLevelDestinationId(destination: TopLevelDestination): Int = when (destination) {
        TopLevelDestination.HOME -> R.id.homeDashboardFragment
        TopLevelDestination.STREAM -> R.id.streamFragment
        TopLevelDestination.VIEW -> R.id.viewFragment
    }
}
