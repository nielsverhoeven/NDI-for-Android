package com.ndi.feature.ndibrowser.settings

import com.ndi.core.model.SettingsLayoutMode
import com.ndi.feature.ndibrowser.domain.repository.SettingsLayoutModeResolver

object SettingsLayoutResolver : SettingsLayoutModeResolver {

    private const val WIDE_LAYOUT_BREAKPOINT_DP = 600
    private const val COMPACT_HEIGHT_PROFILE_MAX_WIDTH_DP = 430

    override fun resolve(widthDp: Int, isLandscape: Boolean): SettingsLayoutMode {
        val isWide = widthDp >= WIDE_LAYOUT_BREAKPOINT_DP
        return if (isWide) {
            SettingsLayoutMode.WIDE
        } else {
            SettingsLayoutMode.COMPACT
        }
    }

    fun isWideLayout(widthDp: Int, isLandscape: Boolean): Boolean {
        return resolve(widthDp, isLandscape) == SettingsLayoutMode.WIDE
    }

    fun isCompactHeightPhoneProfile(widthDp: Int, isLandscape: Boolean): Boolean {
        return isLandscape && widthDp <= COMPACT_HEIGHT_PROFILE_MAX_WIDTH_DP
    }
}
