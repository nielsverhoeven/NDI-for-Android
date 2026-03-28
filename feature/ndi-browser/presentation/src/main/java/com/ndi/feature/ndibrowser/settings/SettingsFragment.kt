package com.ndi.feature.ndibrowser.settings

import android.content.res.Configuration
import android.os.Bundle
import android.net.Uri
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import android.widget.LinearLayout
import android.widget.TextView
import androidx.core.view.isVisible
import androidx.fragment.app.Fragment
import androidx.fragment.app.viewModels
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.lifecycleScope
import androidx.lifecycle.repeatOnLifecycle
import androidx.navigation.fragment.findNavController
import androidx.recyclerview.widget.LinearLayoutManager
import androidx.recyclerview.widget.RecyclerView
import com.ndi.feature.ndibrowser.presentation.R
import com.ndi.core.model.TelemetryEvent
import com.ndi.core.model.SettingsCategoryState
import com.ndi.core.model.SettingsDetailState
import com.ndi.core.model.SettingsLayoutMode
import com.ndi.feature.ndibrowser.presentation.databinding.FragmentSettingsBinding
import kotlinx.coroutines.launch

data class SettingsUiState(
    val developerModeEnabled: Boolean = false,
    val fallbackWarning: String? = null,
    val layoutMode: SettingsLayoutMode = SettingsLayoutMode.COMPACT,
    val settingsCategoryState: SettingsCategoryState = SettingsCategoryState(
        categories = emptyList(),
        selectedCategoryId = null,
        selectionSource = com.ndi.core.model.SettingsCategorySelectionSource.DEFAULT,
    ),
    val settingsDetailState: SettingsDetailState = SettingsDetailState(
        selectedCategoryId = null,
        groups = emptyList(),
        emptyStateMessage = null,
        isEditable = false,
    ),
)

class SettingsFragment : Fragment() {

    private var binding: FragmentSettingsBinding? = null
    private val viewModel: SettingsViewModel by viewModels {
        SettingsViewModel.Factory(
            settingsRepository = SettingsDependencies.requireSettingsRepository(),
            layoutResolver = SettingsLayoutResolver,
        )
    }
    private lateinit var screen: SettingsScreen
    private lateinit var settingsCategoryAdapter: SettingsCategoryAdapter
    private var detailRenderer: SettingsDetailRenderer? = null

    override fun onCreateView(
        inflater: LayoutInflater,
        container: ViewGroup?,
        savedInstanceState: Bundle?,
    ): View {
        val fragmentBinding = FragmentSettingsBinding.inflate(inflater, container, false)
        binding = fragmentBinding
        settingsCategoryAdapter = SettingsCategoryAdapter(viewModel::onSettingsCategorySelected)
        screen = SettingsScreen(
            binding = fragmentBinding,
            onSave = viewModel::onSaveSettings,
            onDeveloperModeToggled = viewModel::onDeveloperModeToggled,
            onOpenThemeEditor = {
                findNavController().navigate(Uri.parse("ndi://theme-editor"))
            },
        )
        val categoriesList = fragmentBinding.root.findViewById<RecyclerView>(R.id.settingsCategoriesList)
        categoriesList.layoutManager = LinearLayoutManager(requireContext())
        categoriesList.adapter = settingsCategoryAdapter
        detailRenderer = SettingsDetailRenderer(
            detailTitle = fragmentBinding.root.findViewById<TextView>(R.id.settingsDetailTitle),
            detailContent = fragmentBinding.root.findViewById<LinearLayout>(R.id.settingsDetailContent),
            detailEmptyState = fragmentBinding.root.findViewById<TextView>(R.id.settingsDetailEmptyState),
            onSave = viewModel::onSaveSettings,
            onOpenThemeEditor = {
                findNavController().navigate(Uri.parse("ndi://theme-editor"))
            },
            onOpenDiscoveryServers = {
                findNavController().navigate(Uri.parse("ndi://settings/discovery-servers"))
            },
            onDeveloperModeToggled = viewModel::onDeveloperModeToggled,
        )
        fragmentBinding.openDiscoveryServersButton.setOnClickListener {
            findNavController().navigate(Uri.parse("ndi://settings/discovery-servers"))
        }
        return fragmentBinding.root
    }

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)
        viewLifecycleOwner.lifecycleScope.launch {
            viewLifecycleOwner.repeatOnLifecycle(Lifecycle.State.STARTED) {
                launch {
                    viewModel.uiState.collect { state ->
                        screen.renderCompact(state)
                        renderTwoColumn(state)
                    }
                }
                launch {
                    viewModel.closeSettingsEvents.collect {
                        val popped = runCatching { findNavController().popBackStack() }.getOrDefault(false)
                        if (!popped) {
                            SettingsDependencies.settingsNavigationBackProvider?.invoke()
                        }
                    }
                }
            }
        }

        viewModel.onLayoutContextChanged(
            widthDp = resources.configuration.screenWidthDp,
            isLandscape = resources.configuration.orientation == Configuration.ORIENTATION_LANDSCAPE,
        )
    }

    override fun onResume() {
        super.onResume()
        viewModel.onCloseSettingsSettled()
        SettingsDependencies.telemetryEmitter.emit(
            TelemetryEvent(
                name = TelemetryEvent.SETTINGS_OPENED,
                timestampEpochMillis = System.currentTimeMillis(),
            ),
        )
    }

    override fun onPause() {
        SettingsDependencies.telemetryEmitter.emit(
            TelemetryEvent(
                name = TelemetryEvent.SETTINGS_CLOSED,
                timestampEpochMillis = System.currentTimeMillis(),
            ),
        )
        super.onPause()
    }

    override fun onDestroyView() {
        detailRenderer = null
        binding = null
        super.onDestroyView()
    }

    private fun renderTwoColumn(state: SettingsUiState) {
        val fragmentBinding = binding ?: return
        val showWide = state.layoutMode == SettingsLayoutMode.WIDE
        fragmentBinding.settingsCompactContainer.isVisible = !showWide
        fragmentBinding.root.findViewById<View>(R.id.settingsTwoColumnContainer).isVisible = showWide
        if (!showWide) return

        settingsCategoryAdapter.submitCategories(state.settingsCategoryState.categories)
        detailRenderer?.render(
            state = state.settingsDetailState,
            developerModeEnabled = state.developerModeEnabled,
        )
    }
}

class SettingsScreen(
    private val binding: FragmentSettingsBinding,
    onSave: () -> Unit,
    onDeveloperModeToggled: (Boolean) -> Unit,
    onOpenThemeEditor: () -> Unit,
) {
    init {
        binding.settingsTopAppBar.inflateMenu(R.menu.settings_menu)
        binding.saveSettingsButton.setOnClickListener { onSave() }
        binding.openThemeEditorButton.setOnClickListener { onOpenThemeEditor() }
        binding.developerModeSwitch.setOnCheckedChangeListener { _, isChecked ->
            onDeveloperModeToggled(isChecked)
        }
    }

    fun renderCompact(state: SettingsUiState) {
        binding.fallbackWarningMessage.isVisible = state.fallbackWarning != null
        binding.fallbackWarningMessage.text = state.fallbackWarning.orEmpty()
        if (binding.developerModeSwitch.isChecked != state.developerModeEnabled) {
            binding.developerModeSwitch.isChecked = state.developerModeEnabled
        }
    }
}
