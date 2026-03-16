package com.ndi.feature.ndibrowser.viewer

import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import androidx.core.view.isVisible
import androidx.fragment.app.Fragment
import androidx.fragment.app.viewModels
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.lifecycleScope
import androidx.lifecycle.repeatOnLifecycle
import androidx.navigation.fragment.findNavController
import com.ndi.feature.ndibrowser.presentation.R
import com.ndi.feature.ndibrowser.presentation.databinding.FragmentViewerBinding
import kotlinx.coroutines.launch

class ViewerFragment : Fragment() {

    private var binding: FragmentViewerBinding? = null

    private val viewModel: ViewerViewModel by viewModels {
        ViewerViewModel.Factory(
            ViewerDependencies.requireViewerRepository(),
            ViewerDependencies.requireUserSelectionRepository(),
        )
    }

    override fun onCreateView(
        inflater: LayoutInflater,
        container: ViewGroup?,
        savedInstanceState: Bundle?,
    ): View {
        val fragmentBinding = FragmentViewerBinding.inflate(inflater, container, false)
        binding = fragmentBinding
        fragmentBinding.retryButton.setOnClickListener { viewModel.onRetryPressed() }
        fragmentBinding.backToListButton.setOnClickListener {
            viewModel.onBackToListPressed()
            runCatching { findNavController().popBackStack() }
        }
        return fragmentBinding.root
    }

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)
        viewModel.onLayoutMeasured(resources.configuration.screenWidthDp)
        viewModel.onViewerOpened(arguments?.getString("sourceId").orEmpty())

        viewLifecycleOwner.lifecycleScope.launch {
            viewLifecycleOwner.repeatOnLifecycle(Lifecycle.State.STARTED) {
                viewModel.uiState.collect { state ->
                    val fragmentBinding = binding ?: return@collect
                    fragmentBinding.viewerTitle.text = getString(R.string.ndi_viewer_title, state.sourceId)
                    fragmentBinding.viewerState.text = state.playbackState.name
                    fragmentBinding.tabletBadge.isVisible = state.layoutMode == ViewerLayoutMode.TABLET
                    fragmentBinding.recoveryMessage.text = state.interruptionMessage
                    fragmentBinding.recoveryMessage.isVisible = state.interruptionMessage != null
                    fragmentBinding.retryButton.isVisible = state.recoveryActionsVisible
                }
            }
        }
    }

    override fun onDestroyView() {
        binding = null
        super.onDestroyView()
    }
}
