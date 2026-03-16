package com.ndi.feature.ndibrowser.output

import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import androidx.fragment.app.viewModels
import androidx.fragment.app.Fragment
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.lifecycleScope
import androidx.lifecycle.repeatOnLifecycle
import com.ndi.feature.ndibrowser.presentation.databinding.FragmentOutputControlBinding
import kotlinx.coroutines.launch

class OutputControlFragment : Fragment() {

    private var binding: FragmentOutputControlBinding? = null

    private val viewModel: OutputControlViewModel by viewModels {
        OutputControlViewModel.Factory(
            OutputDependencies.requireOutputRepository(),
            OutputDependencies.requireOutputConfigurationRepository(),
        )
    }

    private lateinit var screen: OutputControlScreen

    override fun onCreateView(
        inflater: LayoutInflater,
        container: ViewGroup?,
        savedInstanceState: Bundle?,
    ): View {
        val fragmentBinding = FragmentOutputControlBinding.inflate(inflater, container, false)
        binding = fragmentBinding
        screen = OutputControlScreen(
            binding = fragmentBinding,
            onStartPressed = {
                viewModel.onStreamNameChanged(screen.currentStreamName())
                viewModel.onStartOutputPressed()
            },
            onStopPressed = {
                viewModel.onStopOutputPressed()
            },
            onRetryPressed = {
                viewModel.onRetryOutputPressed()
            },
        )
        return fragmentBinding.root
    }

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)
        viewModel.onOutputScreenVisible(arguments?.getString("sourceId").orEmpty())

        viewLifecycleOwner.lifecycleScope.launch {
            viewLifecycleOwner.repeatOnLifecycle(Lifecycle.State.STARTED) {
                viewModel.uiState.collect { state ->
                    binding ?: return@collect
                    screen.render(state)
                }
            }
        }
    }

    override fun onDestroyView() {
        binding = null
        super.onDestroyView()
    }
}
