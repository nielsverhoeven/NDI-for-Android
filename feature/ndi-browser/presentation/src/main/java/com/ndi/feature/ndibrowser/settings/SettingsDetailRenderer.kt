package com.ndi.feature.ndibrowser.settings

import android.widget.LinearLayout
import android.widget.TextView
import androidx.core.view.isVisible
import com.google.android.material.button.MaterialButton
import com.google.android.material.materialswitch.MaterialSwitch
import com.ndi.core.model.SettingsDetailState
import com.ndi.feature.ndibrowser.presentation.R

class SettingsDetailRenderer(
    private val detailTitle: TextView,
    private val detailContent: LinearLayout,
    private val detailEmptyState: TextView,
    private val onSave: () -> Unit,
    private val onOpenThemeEditor: () -> Unit,
    private val onOpenDiscoveryServers: () -> Unit,
    private val onDeveloperModeToggled: (Boolean) -> Unit,
) {

    fun render(
        state: SettingsDetailState,
        developerModeEnabled: Boolean,
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
                val button = MaterialButton(context).apply {
                    text = context.getString(R.string.settings_save_button)
                    setOnClickListener { onSave() }
                }
                detailContent.addView(button)
            }
            SettingsViewModel.CATEGORY_APPEARANCE -> {
                val button = MaterialButton(context).apply {
                    text = context.getString(R.string.settings_open_theme_editor)
                    setOnClickListener { onOpenThemeEditor() }
                }
                detailContent.addView(button)
            }
            SettingsViewModel.CATEGORY_DISCOVERY -> {
                val button = MaterialButton(context).apply {
                    text = context.getString(R.string.discovery_servers_open_button)
                    setOnClickListener { onOpenDiscoveryServers() }
                }
                detailContent.addView(button)
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
