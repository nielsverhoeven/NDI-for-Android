package com.ndi.feature.ndibrowser.viewer

enum class ViewerLayoutMode {
    PHONE,
    TABLET,
}

object ViewerAdaptiveLayout {

    fun resolve(widthDp: Int): ViewerLayoutMode {
        return if (widthDp >= 600) ViewerLayoutMode.TABLET else ViewerLayoutMode.PHONE
    }
}
