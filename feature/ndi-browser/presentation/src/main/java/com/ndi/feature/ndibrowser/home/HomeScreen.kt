package com.ndi.feature.ndibrowser.home

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
import com.ndi.core.model.OutputState
import com.ndi.core.model.PlaybackState
import com.ndi.feature.ndibrowser.presentation.R
import com.ndi.feature.ndibrowser.presentation.databinding.FragmentHomeDashboardBinding
import kotlinx.coroutines.launch

/**
 * Home dashboard fragment — app entry point for launcher launches.
 * Renders aggregate NDI status and provides quick-action buttons to Stream and View.
 */
class HomeDashboardFragment : Fragment(R.layout.fragment_home_dashboard) {

    private var _binding: FragmentHomeDashboardBinding? = null
    private val binding get() = requireNotNull(_binding)

    private val viewModel: HomeViewModel by viewModels {
        HomeViewModel.Factory(HomeDependencies.requireHomeDashboardRepository())
    }

    override fun onCreateView(
        inflater: LayoutInflater,
        container: ViewGroup?,
        savedInstanceState: Bundle?,
    ): View {
        _binding = FragmentHomeDashboardBinding.inflate(inflater, container, false)
        return binding.root
    }

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)

        binding.openStreamButton.setOnClickListener { viewModel.onOpenStreamActionPressed() }
        binding.openViewButton.setOnClickListener { viewModel.onOpenViewActionPressed() }

        viewLifecycleOwner.lifecycleScope.launch {
            viewLifecycleOwner.repeatOnLifecycle(Lifecycle.State.STARTED) {
                launch { viewModel.uiState.collect { renderState(it) } }
                launch { viewModel.navigationEvents.collect { handleNavigationEvent(it) } }
            }
        }
    }

    override fun onStart() {
        super.onStart()
        viewModel.onHomeVisible()
    }

    override fun onDestroyView() {
        super.onDestroyView()
        _binding = null
    }

    private fun renderState(state: HomeUiState) {
        val snap = state.snapshot ?: return

        binding.streamStatusValue.text = when (snap.streamStatus) {
            OutputState.ACTIVE -> getString(R.string.home_stream_status_active)
            else -> getString(R.string.home_stream_status_idle)
        }

        binding.viewStatusValue.text = when (snap.viewPlaybackStatus) {
            PlaybackState.PLAYING -> getString(R.string.home_view_status_playing)
            else -> getString(R.string.home_view_status_stopped)
        }

        val displayName = snap.selectedViewSourceDisplayName
        binding.selectedSourceDisplay.isVisible = !displayName.isNullOrBlank()
        binding.selectedSourceDisplay.text = displayName.orEmpty()

        binding.openStreamButton.isEnabled = snap.canNavigateToStream
        binding.openViewButton.isEnabled = snap.canNavigateToView
    }

    private fun handleNavigationEvent(event: HomeNavigationEvent) {
        // Navigation events are routed upward to the top-level navigation host via the activity.
        // The activity observes these events through a shared interface or callback.
        // Direct NavController access happens in MainActivity to keep fragment navigation-agnostic.
        (activity as? HomeNavigationCallback)?.onHomeNavigationEvent(event)
    }
}

/** Callback interface implemented by MainActivity to handle Home navigation events. */
interface HomeNavigationCallback {
    fun onHomeNavigationEvent(event: HomeNavigationEvent)
}

