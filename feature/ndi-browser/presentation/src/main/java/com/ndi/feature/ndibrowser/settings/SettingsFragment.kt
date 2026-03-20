package com.ndi.feature.ndibrowser.settings

import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import androidx.fragment.app.Fragment
import androidx.navigation.fragment.findNavController
import com.ndi.core.model.TelemetryEvent
import com.ndi.feature.ndibrowser.presentation.databinding.FragmentSettingsBinding

data class SettingsUiState(
    val discoveryServerInput: String = "",
    val developerModeEnabled: Boolean = false,
    val validationError: String? = null,
    val fallbackWarning: String? = null,
)

class SettingsFragment : Fragment() {

    private var binding: FragmentSettingsBinding? = null
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
            onSave = { /* US2: implement save logic */ },
        )
        return fragmentBinding.root
    }

    override fun onResume() {
        super.onResume()
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
) {
    init {
        binding.saveSettingsButton.setOnClickListener { onSave() }
    }

    fun render(state: SettingsUiState) {
        // US2: bind state to views (discoveryServerInput, developerModeSwitch, etc.)
    }
}
