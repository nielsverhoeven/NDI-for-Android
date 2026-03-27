package com.ndi.feature.themeeditor

import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import androidx.fragment.app.Fragment
import androidx.fragment.app.viewModels
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.lifecycleScope
import androidx.lifecycle.repeatOnLifecycle
import com.ndi.core.model.NdiThemeMode
import com.ndi.feature.themeeditor.domain.model.ThemeAccentPalette
import com.ndi.feature.themeeditor.presentation.databinding.FragmentThemeEditorBinding
import kotlinx.coroutines.launch

class ThemeEditorFragment : Fragment() {

    private var binding: FragmentThemeEditorBinding? = null
    private val viewModel: ThemeEditorViewModel by viewModels {
        ThemeEditorViewModel.Factory(ThemeEditorDependencies.requireThemeEditorRepository())
    }

    override fun onCreateView(
        inflater: LayoutInflater,
        container: ViewGroup?,
        savedInstanceState: Bundle?,
    ): View {
        val fragmentBinding = FragmentThemeEditorBinding.inflate(inflater, container, false)
        binding = fragmentBinding
        setupInteractions(fragmentBinding)
        return fragmentBinding.root
    }

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)
        viewLifecycleOwner.lifecycleScope.launch {
            viewLifecycleOwner.repeatOnLifecycle(Lifecycle.State.STARTED) {
                viewModel.uiState.collect { state ->
                    val currentBinding = binding ?: return@collect
                    currentBinding.themeModeGroup.check(
                        when (state.selectedThemeMode) {
                            NdiThemeMode.LIGHT -> currentBinding.themeModeLight.id
                            NdiThemeMode.DARK -> currentBinding.themeModeDark.id
                            NdiThemeMode.SYSTEM -> currentBinding.themeModeSystem.id
                        },
                    )
                    currentBinding.accentPaletteGroup.check(
                        when (state.selectedAccentColorId) {
                            ThemeAccentPalette.ACCENT_BLUE -> currentBinding.accentBlue.id
                            ThemeAccentPalette.ACCENT_TEAL -> currentBinding.accentTeal.id
                            ThemeAccentPalette.ACCENT_GREEN -> currentBinding.accentGreen.id
                            ThemeAccentPalette.ACCENT_ORANGE -> currentBinding.accentOrange.id
                            ThemeAccentPalette.ACCENT_RED -> currentBinding.accentRed.id
                            ThemeAccentPalette.ACCENT_PINK -> currentBinding.accentPink.id
                            else -> currentBinding.accentTeal.id
                        },
                    )
                }
            }
        }
    }

    private fun setupInteractions(binding: FragmentThemeEditorBinding) {
        binding.themeModeLight.setOnClickListener {
            viewModel.onThemeModeSelected(NdiThemeMode.LIGHT)
        }
        binding.themeModeDark.setOnClickListener {
            viewModel.onThemeModeSelected(NdiThemeMode.DARK)
        }
        binding.themeModeSystem.setOnClickListener {
            viewModel.onThemeModeSelected(NdiThemeMode.SYSTEM)
        }

        binding.accentBlue.setOnClickListener {
            viewModel.onAccentColorSelected(ThemeAccentPalette.ACCENT_BLUE)
        }
        binding.accentTeal.setOnClickListener {
            viewModel.onAccentColorSelected(ThemeAccentPalette.ACCENT_TEAL)
        }
        binding.accentGreen.setOnClickListener {
            viewModel.onAccentColorSelected(ThemeAccentPalette.ACCENT_GREEN)
        }
        binding.accentOrange.setOnClickListener {
            viewModel.onAccentColorSelected(ThemeAccentPalette.ACCENT_ORANGE)
        }
        binding.accentRed.setOnClickListener {
            viewModel.onAccentColorSelected(ThemeAccentPalette.ACCENT_RED)
        }
        binding.accentPink.setOnClickListener {
            viewModel.onAccentColorSelected(ThemeAccentPalette.ACCENT_PINK)
        }
    }

    override fun onDestroyView() {
        binding = null
        super.onDestroyView()
    }
}
