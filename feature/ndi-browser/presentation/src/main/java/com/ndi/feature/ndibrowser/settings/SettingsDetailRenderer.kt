package com.ndi.feature.ndibrowser.settings

import android.content.pm.PackageManager
import android.os.Build
import android.view.View
import android.widget.LinearLayout
import android.widget.RadioGroup
import android.widget.ScrollView
import android.widget.TextView
import androidx.core.view.isVisible
import com.google.android.material.materialswitch.MaterialSwitch
import com.google.android.material.radiobutton.MaterialRadioButton
import com.ndi.core.model.CachedSourceRecord
import com.ndi.core.model.NdiThemeMode
import com.ndi.core.model.SettingsDetailState
import com.ndi.feature.ndibrowser.presentation.R

class SettingsDetailRenderer(
    private val detailTitle: TextView,
    private val detailContent: LinearLayout,
    private val detailEmptyState: TextView,
    private val onDeveloperModeToggled: (Boolean) -> Unit,
    private val onThemeModeChanged: (NdiThemeMode) -> Unit,
    private val onAccentColorChanged: (String) -> Unit,
    private val cachedSources: () -> List<CachedSourceRecord> = { emptyList() },
) {

    fun render(
        state: SettingsDetailState,
        developerModeEnabled: Boolean,
        themeMode: NdiThemeMode,
        accentColorId: String,
    ) {
        detailContent.removeAllViews()
        detailEmptyState.isVisible = state.emptyStateMessage != null
        detailEmptyState.text = state.emptyStateMessage.orEmpty()

        if (state.groups.isEmpty()) {
            return
        }

        val context = detailContent.context
        val group = state.groups.first()
        detailTitle.text = group.title

        when (state.selectedCategoryId) {
            SettingsViewModel.CATEGORY_GENERAL -> {
                val hint = TextView(context).apply {
                    text = context.getString(R.string.settings_detail_general_hint)
                    setTextAppearance(com.google.android.material.R.style.TextAppearance_Material3_BodyMedium)
                }
                detailContent.addView(hint)
            }
            SettingsViewModel.CATEGORY_APPEARANCE -> {
                val radioGroup = RadioGroup(context)
                val modeIds = mapOf(
                    NdiThemeMode.LIGHT to View.generateViewId(),
                    NdiThemeMode.DARK to View.generateViewId(),
                    NdiThemeMode.SYSTEM to View.generateViewId(),
                )
                val idsToMode = modeIds.entries.associate { it.value to it.key }
                listOf(
                    NdiThemeMode.LIGHT to context.getString(R.string.settings_theme_mode_light),
                    NdiThemeMode.DARK to context.getString(R.string.settings_theme_mode_dark),
                    NdiThemeMode.SYSTEM to context.getString(R.string.settings_theme_mode_system),
                ).forEach { (mode, label) ->
                    val radio = MaterialRadioButton(context).apply {
                        text = label
                        id = modeIds[mode]!!
                    }
                    radioGroup.addView(radio)
                }
                radioGroup.check(modeIds[themeMode]!!)
                radioGroup.setOnCheckedChangeListener { _, checkedId ->
                    idsToMode[checkedId]?.let { onThemeModeChanged(it) }
                }
                detailContent.addView(radioGroup)

                val accentLabel = TextView(context).apply {
                    text = context.getString(R.string.settings_accent_color_label)
                    setTextAppearance(com.google.android.material.R.style.TextAppearance_Material3_BodyMedium)
                }
                detailContent.addView(accentLabel)

                val accentGroup = RadioGroup(context)
                val accentIds = mapOf(
                    SettingsViewModel.ACCENT_BLUE to View.generateViewId(),
                    SettingsViewModel.ACCENT_TEAL to View.generateViewId(),
                    SettingsViewModel.ACCENT_GREEN to View.generateViewId(),
                    SettingsViewModel.ACCENT_ORANGE to View.generateViewId(),
                    SettingsViewModel.ACCENT_RED to View.generateViewId(),
                    SettingsViewModel.ACCENT_PINK to View.generateViewId(),
                )
                val idsToAccent = accentIds.entries.associate { it.value to it.key }
                listOf(
                    SettingsViewModel.ACCENT_BLUE to context.getString(R.string.settings_accent_blue),
                    SettingsViewModel.ACCENT_TEAL to context.getString(R.string.settings_accent_teal),
                    SettingsViewModel.ACCENT_GREEN to context.getString(R.string.settings_accent_green),
                    SettingsViewModel.ACCENT_ORANGE to context.getString(R.string.settings_accent_orange),
                    SettingsViewModel.ACCENT_RED to context.getString(R.string.settings_accent_red),
                    SettingsViewModel.ACCENT_PINK to context.getString(R.string.settings_accent_pink),
                ).forEach { (accent, label) ->
                    val radio = MaterialRadioButton(context).apply {
                        text = label
                        id = accentIds[accent]!!
                    }
                    accentGroup.addView(radio)
                }
                accentGroup.check(accentIds[accentColorId] ?: accentIds[SettingsViewModel.ACCENT_TEAL]!!)
                accentGroup.setOnCheckedChangeListener { _, checkedId ->
                    idsToAccent[checkedId]?.let { onAccentColorChanged(it) }
                }
                detailContent.addView(accentGroup)
            }
            SettingsViewModel.CATEGORY_DISCOVERY -> {
                // Discovery servers are managed inline via an embedded fragment.
            }
            SettingsViewModel.CATEGORY_DEVELOPER -> {
                val toggle = MaterialSwitch(context).apply {
                    text = context.getString(R.string.settings_developer_mode_label)
                    isChecked = developerModeEnabled
                    setOnCheckedChangeListener { _, checked ->
                        onDeveloperModeToggled(checked)
                    }
                }
                detailContent.addView(toggle)

                if (developerModeEnabled) {
                    // Cached source database inspection (read-only, Feature 030 US3)
                    val dbHeader = TextView(context).apply {
                        text = "Cached Source Registry"
                        setTextAppearance(com.google.android.material.R.style.TextAppearance_Material3_TitleSmall)
                        setPadding(0, 32, 0, 8)
                    }
                    detailContent.addView(dbHeader)

                    val sources = cachedSources()
                    if (sources.isEmpty()) {
                        val empty = TextView(context).apply {
                            text = "No cached sources yet. Discover NDI sources via the Source List to populate the registry."
                            setTextAppearance(com.google.android.material.R.style.TextAppearance_Material3_BodyMedium)
                        }
                        detailContent.addView(empty)
                    } else {
                        sources.forEach { record ->
                            val card = LinearLayout(context).apply {
                                orientation = LinearLayout.VERTICAL
                                val params = LinearLayout.LayoutParams(
                                    LinearLayout.LayoutParams.MATCH_PARENT,
                                    LinearLayout.LayoutParams.WRAP_CONTENT,
                                )
                                params.setMargins(0, 8, 0, 8)
                                layoutParams = params
                                setBackgroundColor(0x11000000)
                                setPadding(16, 12, 16, 12)
                            }
                            val displayLine = TextView(context).apply {
                                text = record.displayName
                                setTextAppearance(com.google.android.material.R.style.TextAppearance_Material3_LabelLarge)
                            }
                            val endpointLine = TextView(context).apply {
                                text = "Endpoint: ${record.endpointHost}:${record.endpointPort}"
                                setTextAppearance(com.google.android.material.R.style.TextAppearance_Material3_BodySmall)
                            }
                            val stateLine = TextView(context).apply {
                                text = "State: ${record.validationState.name}"
                                setTextAppearance(com.google.android.material.R.style.TextAppearance_Material3_BodySmall)
                            }
                            val keyLine = TextView(context).apply {
                                text = "Key: ${record.cacheKey}"
                                setTextAppearance(com.google.android.material.R.style.TextAppearance_Material3_BodySmall)
                            }
                            val tsLine = TextView(context).apply {
                                text = "Last seen: ${java.text.SimpleDateFormat("yyyy-MM-dd HH:mm:ss", java.util.Locale.US).format(java.util.Date(record.lastDiscoveredAtEpochMillis))}"
                                setTextAppearance(com.google.android.material.R.style.TextAppearance_Material3_BodySmall)
                            }
                            card.addView(displayLine)
                            card.addView(endpointLine)
                            card.addView(stateLine)
                            card.addView(keyLine)
                            card.addView(tsLine)
                            detailContent.addView(card)
                        }
                    }
                }
            }
            SettingsViewModel.CATEGORY_ABOUT -> {
                val label = TextView(context).apply {
                    text = context.getString(R.string.settings_about_version_label)
                    setTextAppearance(com.google.android.material.R.style.TextAppearance_Material3_BodyMedium)
                }
                val value = TextView(context).apply {
                    text = resolveAppVersionText()
                    setTextAppearance(com.google.android.material.R.style.TextAppearance_Material3_TitleSmall)
                }
                detailContent.addView(label)
                detailContent.addView(value)
            }
            else -> {
                // Empty-state category has no controls.
            }
        }
    }

    private fun resolveAppVersionText(): String {
        val context = detailContent.context
        return try {
            val packageInfo = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) {
                context.packageManager.getPackageInfo(
                    context.packageName,
                    PackageManager.PackageInfoFlags.of(0),
                )
            } else {
                @Suppress("DEPRECATION")
                context.packageManager.getPackageInfo(context.packageName, 0)
            }
            val versionName = packageInfo.versionName.orEmpty().ifBlank {
                context.getString(R.string.settings_about_version_unavailable)
            }
            val versionCode = packageInfo.longVersionCode
            context.getString(R.string.settings_about_version_value, versionName, versionCode)
        } catch (_: Exception) {
            context.getString(R.string.settings_about_version_unavailable)
        }
    }
}
