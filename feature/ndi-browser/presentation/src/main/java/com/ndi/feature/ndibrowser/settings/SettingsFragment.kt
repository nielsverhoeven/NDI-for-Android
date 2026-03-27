package com.ndi.feature.ndibrowser.settings

import android.os.Bundle
import android.net.Uri
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import androidx.core.widget.addTextChangedListener
import androidx.core.view.isVisible
import androidx.fragment.app.Fragment
import androidx.fragment.app.viewModels
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.lifecycleScope
import androidx.lifecycle.repeatOnLifecycle
import androidx.navigation.fragment.findNavController
import com.ndi.feature.ndibrowser.presentation.R
import com.ndi.core.model.TelemetryEvent
import com.ndi.feature.ndibrowser.presentation.databinding.FragmentSettingsBinding
import kotlinx.coroutines.launch

data class SettingsUiState(
    val discoveryServerInput: String = "",
    val developerModeEnabled: Boolean = false,
    val validationError: String? = null,
    val fallbackWarning: String? = null,
)

class SettingsFragment : Fragment() {

    private var binding: FragmentSettingsBinding? = null
    private val viewModel: SettingsViewModel by viewModels {
        SettingsViewModel.Factory(SettingsDependencies.requireSettingsRepository())
    }
    private lateinit var screen: SettingsScreen

    override fun onCreateView(
        inflater: LayoutInflater,
        container: ViewGroup?,
        savedInstanceState: Bundle?,
    ): View {
        val fragmentBinding = FragmentSettingsBinding.inflate(inflater, container, false)
        binding = fragmentBinding
        screen = SettingsScreen(
            binding = fragmentBinding,
            onSave = viewModel::onSaveSettings,
            onDiscoveryChanged = viewModel::onDiscoveryServerChanged,
            onDeveloperModeToggled = viewModel::onDeveloperModeToggled,
            onOpenThemeEditor = {
                findNavController().navigate(Uri.parse("ndi://theme-editor"))
            },
        )
        return fragmentBinding.root
    }

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)
        viewLifecycleOwner.lifecycleScope.launch {
            viewLifecycleOwner.repeatOnLifecycle(Lifecycle.State.STARTED) {
                launch {
                    viewModel.uiState.collect { screen.render(it) }
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
        binding = null
        super.onDestroyView()
    }
}

class SettingsScreen(
    private val binding: FragmentSettingsBinding,
    onSave: () -> Unit,
    onDiscoveryChanged: (String) -> Unit,
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
        binding.discoveryServerEditText.addTextChangedListener {
            onDiscoveryChanged(it?.toString().orEmpty())
        }
    }

    fun render(state: SettingsUiState) {
        binding.validationMessage.isVisible = state.validationError != null
        binding.validationMessage.text = state.validationError.orEmpty()
        binding.fallbackWarningMessage.isVisible = state.fallbackWarning != null
        binding.fallbackWarningMessage.text = state.fallbackWarning.orEmpty()
        if (binding.developerModeSwitch.isChecked != state.developerModeEnabled) {
            binding.developerModeSwitch.isChecked = state.developerModeEnabled
        }
        val currentInput = binding.discoveryServerInput.editText?.text?.toString().orEmpty()
        if (currentInput != state.discoveryServerInput) {
            binding.discoveryServerInput.editText?.setText(state.discoveryServerInput)
        }
    }
}
