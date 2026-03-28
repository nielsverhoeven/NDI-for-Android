package com.ndi.feature.ndibrowser.settings

import android.view.View
import android.widget.LinearLayout
import android.widget.RadioGroup
import android.widget.TextView
import androidx.core.view.isVisible
import com.google.android.material.button.MaterialButton
import com.google.android.material.materialswitch.MaterialSwitch
import com.google.android.material.radiobutton.MaterialRadioButton
import com.ndi.core.model.NdiThemeMode
import com.ndi.core.model.SettingsDetailState
import com.ndi.feature.ndibrowser.presentation.R

class SettingsDetailRenderer(
    private val detailTitle: TextView,
    private val detailContent: LinearLayout,
    private val detailEmptyState: TextView,
    private val onDeveloperModeToggled: (Boolean) -> Unit,
    private val onThemeModeChanged: (NdiThemeMode) -> Unit,
) {

    fun render(
        state: SettingsDetailState,
        developerModeEnabled: Boolean,
        themeMode: NdiThemeMode,
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
            }
            else -> {
                // Empty-state category has no controls.
            }
        }
    }
}
