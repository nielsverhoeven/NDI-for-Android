package com.ndi.feature.themeeditor.data.repository

import com.ndi.core.model.NdiSettingsSnapshot
import com.ndi.core.model.NdiThemeMode
import com.ndi.feature.themeeditor.domain.model.ThemeAccentPalette
import com.ndi.feature.themeeditor.domain.model.ThemePreference

internal object ThemePreferenceMapper {

    fun fromSettings(snapshot: NdiSettingsSnapshot): ThemePreference {
        val normalizedAccent = normalizeAccent(snapshot.accentColorId)
        return ThemePreference(
            themeMode = snapshot.themeMode,
            accentColorId = normalizedAccent,
            updatedAtEpochMillis = snapshot.updatedAtEpochMillis,
        )
    }

    fun toSettings(current: NdiSettingsSnapshot, preference: ThemePreference): NdiSettingsSnapshot {
        return current.copy(
            themeMode = preference.themeMode,
            accentColorId = normalizeAccent(preference.accentColorId),
            updatedAtEpochMillis = preference.updatedAtEpochMillis,
        )
    }

    fun normalizeAccent(accentColorId: String): String {
        return if (ThemeAccentPalette.curatedOptionIds.contains(accentColorId)) {
            accentColorId
        } else {
            ThemeAccentPalette.defaultAccentColorId
        }
    }

    fun normalizeThemeMode(raw: String?): NdiThemeMode {
        if (raw.isNullOrBlank()) {
            return NdiThemeMode.SYSTEM
        }
        return runCatching { NdiThemeMode.valueOf(raw) }.getOrDefault(NdiThemeMode.SYSTEM)
    }
}
