package com.ndi.feature.ndibrowser.viewer

import android.graphics.BitmapFactory
import android.os.Bundle
import android.view.LayoutInflater
import android.view.View
import android.view.ViewGroup
import androidx.core.net.toUri
import androidx.core.view.isVisible
import androidx.fragment.app.Fragment
import androidx.fragment.app.viewModels
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.lifecycleScope
import androidx.lifecycle.repeatOnLifecycle
import androidx.navigation.NavDeepLinkRequest
import androidx.navigation.fragment.findNavController
import com.ndi.core.model.PlaybackState
import com.ndi.feature.ndibrowser.presentation.R
import com.ndi.feature.ndibrowser.presentation.databinding.FragmentViewerBinding
import java.net.HttpURLConnection
import java.net.URL
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.isActive
import kotlinx.coroutines.launch
import kotlinx.coroutines.withContext

class ViewerFragment : Fragment() {

    private var binding: FragmentViewerBinding? = null
    private var relayPreviewJob: Job? = null
    private var relayPreviewSourceId: String? = null

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
            fragmentBinding.viewerTopAppBar.inflateMenu(R.menu.viewer_menu)
            fragmentBinding.viewerTopAppBar.setOnMenuItemClickListener { item ->
                if (item.itemId == R.id.action_settings) {
                    runCatching {
                        findNavController().navigate(
                            NavDeepLinkRequest.Builder.fromUri("ndi://settings".toUri()).build(),
                        )
                    }
                    true
                } else {
                    false
                }
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
                    renderRelayPreview(state.sourceId, state.playbackState)
                }
            }
        }
    }

    private fun renderRelayPreview(sourceId: String, playbackState: PlaybackState) {
        val fragmentBinding = binding ?: return
        val canRenderRelay = sourceId.startsWith("relay-screen:") && playbackState == PlaybackState.PLAYING
        if (!canRenderRelay) {
            relayPreviewJob?.cancel()
            relayPreviewJob = null
            relayPreviewSourceId = null
            fragmentBinding.viewerPreviewImage.setImageDrawable(null)
            return
        }

        if (relayPreviewJob != null && relayPreviewSourceId == sourceId) {
            return
        }

        relayPreviewJob?.cancel()
        relayPreviewSourceId = sourceId
        relayPreviewJob = viewLifecycleOwner.lifecycleScope.launch {
            while (isActive) {
                val bitmap = withContext(Dispatchers.IO) {
                    runCatching {
                        val encodedSourceId = java.net.URLEncoder.encode(sourceId, Charsets.UTF_8.name())
                        val url = URL("http://10.0.2.2:17455/frame/$encodedSourceId")
                        val connection = (url.openConnection() as HttpURLConnection).apply {
                            requestMethod = "GET"
                            connectTimeout = 1_000
                            readTimeout = 1_500
                        }
                        try {
                            if (connection.responseCode !in 200..299) return@runCatching null
                            connection.inputStream.use { input -> BitmapFactory.decodeStream(input) }
                        } finally {
                            connection.disconnect()
                        }
                    }.getOrNull()
                }

                if (bitmap != null) {
                    binding?.viewerPreviewImage?.setImageBitmap(bitmap)
                } else {
                    // Clear stale frame immediately when publisher has stopped or no relay frame is available.
                    binding?.viewerPreviewImage?.setImageDrawable(null)
                }
                delay(750)
            }
        }
    }

    override fun onDestroyView() {
        relayPreviewJob?.cancel()
        relayPreviewJob = null
        relayPreviewSourceId = null
        binding = null
        super.onDestroyView()
    }
}
