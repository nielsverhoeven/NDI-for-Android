package com.ndi.feature.themeeditor.domain.repository

import com.ndi.feature.themeeditor.domain.model.ThemePreference
import kotlinx.coroutines.flow.Flow

interface ThemeEditorRepository {
    suspend fun getThemePreference(): ThemePreference
    suspend fun saveThemePreference(preference: ThemePreference)
    fun observeThemePreference(): Flow<ThemePreference>
}
