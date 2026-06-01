package com.ndi.feature.ndibrowser.source_list

enum class SourceListLayoutMode {
    COMPACT,
    EXPANDED,
}

object SourceListAdaptiveLayout {

    private const val ExpandedWidthDp = 600

    fun resolve(widthDp: Int): SourceListLayoutMode {
        return if (widthDp >= ExpandedWidthDp) {
            SourceListLayoutMode.EXPANDED
        } else {
            SourceListLayoutMode.COMPACT
        }
    }
}
