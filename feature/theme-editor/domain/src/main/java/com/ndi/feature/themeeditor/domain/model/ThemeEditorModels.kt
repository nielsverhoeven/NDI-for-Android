package com.ndi.feature.themeeditor.domain.model

import com.ndi.core.model.NdiThemeMode

data class ThemePreference(
    val themeMode: NdiThemeMode,
    val accentColorId: String,
    val updatedAtEpochMillis: Long,
)

data class AccentPaletteOption(
    val id: String,
    val displayLabel: String,
)

object ThemeAccentPalette {
    const val ACCENT_BLUE = "accent_blue"
    const val ACCENT_TEAL = "accent_teal"
    const val ACCENT_GREEN = "accent_green"
    const val ACCENT_ORANGE = "accent_orange"
    const val ACCENT_RED = "accent_red"
    const val ACCENT_PINK = "accent_pink"

    val curatedOptionIds: Set<String> = setOf(
        ACCENT_BLUE,
        ACCENT_TEAL,
        ACCENT_GREEN,
        ACCENT_ORANGE,
        ACCENT_RED,
        ACCENT_PINK,
    )

    const val defaultAccentColorId: String = ACCENT_TEAL
}
