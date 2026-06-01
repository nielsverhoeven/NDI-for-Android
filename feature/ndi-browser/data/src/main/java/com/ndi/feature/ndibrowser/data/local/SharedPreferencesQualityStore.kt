package com.ndi.feature.ndibrowser.data.local

import android.content.Context
import com.ndi.feature.ndibrowser.data.model.StoredQualityPreference

class SharedPreferencesQualityStore internal constructor(
    private val prefs: android.content.SharedPreferences,
) {

    constructor(context: Context) : this(
        context.getSharedPreferences(PREF_FILE, Context.MODE_PRIVATE),
    )

    fun saveQualityPreference(preference: StoredQualityPreference) {
        val key = keyFor(preference.sourceId)
        prefs.edit()
            .putString(key, preference.profileId)
            .putLong(TIMESTAMP_KEY_PREFIX + key, preference.timestampEpochMillis)
            .apply()
    }

    fun loadQualityPreference(sourceId: String? = null): StoredQualityPreference {
        val specificKey = keyFor(sourceId)
        val globalKey = keyFor(null)
        val profileId = prefs.getString(specificKey, null)
            ?: prefs.getString(globalKey, DEFAULT_PROFILE_ID)
            ?: DEFAULT_PROFILE_ID
        val timestamp = prefs.getLong(TIMESTAMP_KEY_PREFIX + specificKey, System.currentTimeMillis())
        return StoredQualityPreference(sourceId = sourceId, profileId = profileId, timestampEpochMillis = timestamp)
    }

    fun clearPreferences() {
        prefs.edit().clear().apply()
    }

    fun save(preference: StoredQualityPreference) = saveQualityPreference(preference)

    fun load(sourceId: String? = null): StoredQualityPreference = loadQualityPreference(sourceId)

    fun clear() = clearPreferences()

    private fun keyFor(sourceId: String?): String {
        return if (sourceId.isNullOrBlank()) GLOBAL_KEY else "source_$sourceId"
    }

    private companion object {
        const val PREF_FILE = "ndi_quality_prefs"
        const val GLOBAL_KEY = "global"
        const val DEFAULT_PROFILE_ID = "smooth"
        const val TIMESTAMP_KEY_PREFIX = "ts_"
    }
}
