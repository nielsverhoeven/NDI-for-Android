package com.ndi.feature.ndibrowser.source_list

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
import androidx.recyclerview.widget.GridLayoutManager
import androidx.recyclerview.widget.RecyclerView
import com.ndi.feature.ndibrowser.presentation.R
import com.ndi.feature.ndibrowser.presentation.databinding.FragmentSourceListBinding
import com.ndi.feature.ndibrowser.source_list.adapter.SourceAdapter
import kotlinx.coroutines.launch

class SourceListFragment : Fragment() {

    private var binding: FragmentSourceListBinding? = null

    private val viewModel: SourceListViewModel by viewModels {
        SourceListViewModel.Factory(
            SourceListDependencies.requireDiscoveryRepository(),
            SourceListDependencies.requireUserSelectionRepository(),
        )
    }

    private lateinit var screen: SourceListScreen

    override fun onCreateView(
        inflater: LayoutInflater,
        container: ViewGroup?,
        savedInstanceState: Bundle?,
    ): View {
        val fragmentBinding = FragmentSourceListBinding.inflate(inflater, container, false)
        binding = fragmentBinding
        screen = SourceListScreen(
            binding = fragmentBinding,
            onManualRefresh = viewModel::onManualRefresh,
            onSourceClicked = viewModel::onSourceSelected,
            onOutputClicked = viewModel::onOutputRequested,
        )
        return fragmentBinding.root
    }

    override fun onViewCreated(view: View, savedInstanceState: Bundle?) {
        super.onViewCreated(view, savedInstanceState)
        viewModel.onLayoutMeasured(resources.configuration.screenWidthDp)

        viewLifecycleOwner.lifecycleScope.launch {
            viewLifecycleOwner.repeatOnLifecycle(Lifecycle.State.STARTED) {
                launch { viewModel.uiState.collect(screen::render) }
                launch {
                    viewModel.navigationEvents.collect { sourceId ->
                        runCatching {
                            findNavController().navigate(SourceListDependencies.viewerNavigationRequest(sourceId))
                        }
                    }
                }
                launch {
                    viewModel.outputNavigationEvents.collect { sourceId ->
                        runCatching {
                            findNavController().navigate(SourceListDependencies.outputNavigationRequest(sourceId))
                        }
                    }
                }
            }
        }
    }

    override fun onStart() {
        super.onStart()
        viewModel.onScreenVisible()
    }

    override fun onStop() {
        viewModel.onScreenHidden()
        super.onStop()
    }

    override fun onDestroyView() {
        binding = null
        super.onDestroyView()
    }
}

class SourceListScreen(
    private val binding: FragmentSourceListBinding,
    onManualRefresh: () -> Unit,
    onSourceClicked: (String) -> Unit,
    onOutputClicked: (String) -> Unit,
) {

    private val adapter = SourceAdapter(onSourceClicked, onOutputClicked)

    init {
        binding.sourceRecyclerView.adapter = adapter
        binding.refreshButton.setOnClickListener { onManualRefresh() }
    }

    fun render(state: SourceListUiState) {
        val spanCount = if (state.layoutMode == SourceListLayoutMode.EXPANDED) 2 else 1
        val currentLayoutManager = binding.sourceRecyclerView.layoutManager as? GridLayoutManager
        if (currentLayoutManager == null || currentLayoutManager.spanCount != spanCount) {
            binding.sourceRecyclerView.layoutManager = GridLayoutManager(binding.root.context, spanCount)
        }

        val orderedSources = state.sources.sortedByDescending { it.sourceId.startsWith("device-screen:") }
        adapter.submitList(orderedSources, state.highlightedSourceId)
        binding.progressIndicator.isVisible = state.discoveryStatus == com.ndi.core.model.DiscoveryStatus.IN_PROGRESS
        binding.sourceRecyclerView.isVisible = state.sources.isNotEmpty()
        binding.emptyStateText.isVisible = state.discoveryStatus == com.ndi.core.model.DiscoveryStatus.EMPTY
        binding.errorStateText.isVisible = state.discoveryStatus == com.ndi.core.model.DiscoveryStatus.FAILURE
        binding.errorStateText.text = state.errorMessage ?: binding.root.context.getString(R.string.ndi_discovery_error)
    }

    fun recyclerView(): RecyclerView = binding.sourceRecyclerView
}
