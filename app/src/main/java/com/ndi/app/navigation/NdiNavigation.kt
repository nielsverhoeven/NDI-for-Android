package com.ndi.app.navigation

import android.os.Bundle
import androidx.core.net.toUri
import androidx.navigation.NavDeepLinkRequest

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
}
